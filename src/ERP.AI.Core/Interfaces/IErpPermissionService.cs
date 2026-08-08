namespace ERP.AI.Core.Interfaces;

public interface IErpPermissionService
{
    Task<bool> HasPermissionAsync(string userId, string permission, CancellationToken cancellationToken = default);
}
