using ERP.AI.Core.Interfaces;

namespace ERP.AI.Api.Security;

public sealed class ConfiguredErpPermissionService : IErpPermissionService
{
    private static readonly string[] DemoPermissions =
    {
        "Accounting.View",
        "Sales.View",
        "Inventory.View",
        "Project.View"
    };

    private readonly IConfiguration _configuration;

    public ConfiguredErpPermissionService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public Task<bool> HasPermissionAsync(string userId, string permission, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(permission))
        {
            return Task.FromResult(true);
        }

        var userPermissions = _configuration.GetSection($"Security:Users:{userId}:Permissions").Get<string[]>();
        var permissions = userPermissions is { Length: > 0 }
            ? userPermissions
            : _configuration.GetSection("Security:Permissions").Get<string[]>() ?? Array.Empty<string>();

        if (permissions.Length == 0 && _configuration.GetValue("Security:UseDemoPermissions", true))
        {
            permissions = DemoPermissions;
        }

        var hasPermission = permissions.Any(p =>
            p == "*" || string.Equals(p, permission, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(hasPermission);
    }
}
