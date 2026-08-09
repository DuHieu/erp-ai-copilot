using Microsoft.AspNetCore.Mvc;

namespace ERP.AI.Web.Controllers;

internal static class ProxyResponseResult
{
    public static async Task<ContentResult> FromAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/json";

        return new ContentResult
        {
            Content = content,
            ContentType = contentType,
            StatusCode = (int)response.StatusCode
        };
    }
}
