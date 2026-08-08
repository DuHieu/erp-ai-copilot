using ERP.AI.Core.Interfaces;

namespace ERP.AI.Infrastructure.Security;

public class MockErpPermissionService : IErpPermissionService
{
    private readonly HashSet<string> _grantedPermissions = new(StringComparer.OrdinalIgnoreCase)
    {
        "Accounting.View",
        "Sales.View",
        "Inventory.View",
        "Project.View"
    };

    public Task<bool> HasPermissionAsync(string userId, string permission, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(permission))
        {
            return Task.FromResult(true);
        }

        // Demo user has view access to all 4 ERP domains
        bool hasPerm = _grantedPermissions.Contains(permission);
        return Task.FromResult(hasPerm);
    }
}
