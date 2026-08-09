using System.Security.Claims;
using ERP.AI.Core.Interfaces;

namespace ERP.AI.Api.Security;

public sealed class HttpContextCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IConfiguration _configuration;

    public HttpContextCurrentUser(IHttpContextAccessor httpContextAccessor, IConfiguration configuration)
    {
        _httpContextAccessor = httpContextAccessor;
        _configuration = configuration;
    }

    public string UserId
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            var claimUserId = user?.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!string.IsNullOrWhiteSpace(claimUserId))
            {
                return claimUserId;
            }

            return _configuration["Security:DemoUserId"] ?? "demo-user";
        }
    }

    public IReadOnlyList<string> Roles
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            var claimRoles = user?.FindAll(ClaimTypes.Role)
                .Select(claim => claim.Value)
                .Where(role => !string.IsNullOrWhiteSpace(role))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (claimRoles is { Count: > 0 })
            {
                return claimRoles.AsReadOnly();
            }

            var configuredRoles = _configuration.GetSection("Security:DemoRoles").Get<string[]>();
            if (configuredRoles is { Length: > 0 })
            {
                return configuredRoles.ToList().AsReadOnly();
            }

            return new[] { "Finance", "Manager" };
        }
    }
}
