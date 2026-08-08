namespace ERP.AI.Core.Interfaces;

public interface IErpToolRegistry
{
    IReadOnlyList<IErpTool> GetAllTools();
    IErpTool? GetTool(string toolName);
    Task<(bool Success, object? Result, string? Error)> ExecuteToolAsync(string toolName, string argumentsJson, CancellationToken cancellationToken = default);
}
