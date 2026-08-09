using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace ERP.AI.Api.Security;

public sealed class ApiKeyAuthenticationMiddleware
{
    private const string ApiKeyHeaderName = "X-API-Key";
    private readonly RequestDelegate _next;

    public ApiKeyAuthenticationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IConfiguration configuration)
    {
        if (!configuration.GetValue<bool>("Security:RequireApiKey") ||
            !context.Request.Path.StartsWithSegments("/api"))
        {
            await _next(context);
            return;
        }

        var configuredApiKey = configuration["Security:ApiKey"];
        if (string.IsNullOrWhiteSpace(configuredApiKey))
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(new { error = "API key authentication is enabled but Security:ApiKey is not configured." });
            return;
        }

        if (!TryReadApiKey(context.Request, out var suppliedApiKey) ||
            !FixedTimeEquals(suppliedApiKey, configuredApiKey))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "A valid API key is required for API endpoints." });
            return;
        }

        var userId = configuration["Security:ApiKeyUserId"];
        if (string.IsNullOrWhiteSpace(userId))
        {
            userId = "api-key-user";
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Name, userId)
        };

        var roles = configuration.GetSection("Security:ApiKeyRoles").Get<string[]>() ?? Array.Empty<string>();
        claims.AddRange(roles.Where(role => !string.IsNullOrWhiteSpace(role)).Select(role => new Claim(ClaimTypes.Role, role)));

        context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "ApiKey"));
        await _next(context);
    }

    private static bool TryReadApiKey(HttpRequest request, out string apiKey)
    {
        if (request.Headers.TryGetValue(ApiKeyHeaderName, out var headerValue) &&
            !string.IsNullOrWhiteSpace(headerValue))
        {
            apiKey = headerValue.ToString();
            return true;
        }

        var authorization = request.Headers.Authorization.ToString();
        if (authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            apiKey = authorization["Bearer ".Length..].Trim();
            return !string.IsNullOrWhiteSpace(apiKey);
        }

        apiKey = string.Empty;
        return false;
    }

    private static bool FixedTimeEquals(string supplied, string expected)
    {
        var suppliedBytes = Encoding.UTF8.GetBytes(supplied);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);

        return suppliedBytes.Length == expectedBytes.Length &&
               CryptographicOperations.FixedTimeEquals(suppliedBytes, expectedBytes);
    }
}
