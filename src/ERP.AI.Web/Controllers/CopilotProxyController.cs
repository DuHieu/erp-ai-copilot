using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace ERP.AI.Web.Controllers;

[ApiController]
[Route("api/copilot")]
public class CopilotProxyController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<CopilotProxyController> _logger;

    public CopilotProxyController(IHttpClientFactory httpClientFactory, ILogger<CopilotProxyController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    [HttpPost("chat")]
    public async Task<IActionResult> ProxyChat([FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        const string targetPath = "/api/copilot/chat";
        _logger.LogInformation("Proxying Copilot chat request to backend API path: {TargetPath}", targetPath);

        try
        {
            var httpClient = _httpClientFactory.CreateClient("ErpApi");
            var content = new StringContent(body.GetRawText(), Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync(targetPath, content, cancellationToken);
            return await ProxyResponseResult.FromAsync(response, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while proxying chat request to {TargetPath}", targetPath);
            return StatusCode(503, new
            {
                error = "ERP AI API service is currently unavailable or unreachable.",
                details = ex.Message,
                targetPath = targetPath
            });
        }
    }
}
