using System.Text.Json;
using ERP.AI.Core.Interfaces;

namespace ERP.AI.Copilot.Services;

public class ErpToolRegistry : IErpToolRegistry
{
    private readonly Dictionary<string, IErpTool> _tools;
    private readonly IErpPermissionService _permissionService;
    private readonly ICurrentUser _currentUser;

    public ErpToolRegistry(
        IEnumerable<IErpTool> tools,
        IErpPermissionService permissionService,
        ICurrentUser currentUser)
    {
        _permissionService = permissionService;
        _currentUser = currentUser;
        _tools = tools.ToDictionary(t => t.Name, t => t, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<IErpTool> GetAllTools()
    {
        return _tools.Values.ToList().AsReadOnly();
    }

    public IErpTool? GetTool(string toolName)
    {
        return _tools.TryGetValue(toolName, out var tool) ? tool : null;
    }

    public async Task<(bool Success, object? Result, string? Error)> ExecuteToolAsync(
        string toolName,
        string argumentsJson,
        CancellationToken cancellationToken = default)
    {
        if (!_tools.TryGetValue(toolName, out var tool))
        {
            return (false, null, $"Tool '{toolName}' is not recognized or supported by ERP AI Copilot.");
        }

        // Backend permission enforcement
        if (!string.IsNullOrWhiteSpace(tool.RequiredPermission))
        {
            bool hasPerm = await _permissionService.HasPermissionAsync(_currentUser.UserId, tool.RequiredPermission, cancellationToken);
            if (!hasPerm)
            {
                return (false, null, $"You don't have permission to access {tool.Name} ({tool.RequiredPermission}) information.");
            }
        }

        try
        {
            var result = await tool.ExecuteAsync(argumentsJson, cancellationToken);
            return (true, result, null);
        }
        catch (Exception ex)
        {
            return (false, null, $"Execution of tool '{toolName}' failed: {ex.Message}");
        }
    }
}
