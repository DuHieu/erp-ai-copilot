using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace ERP.AI.Web.Controllers;

[ApiController]
[Route("api/copilot")]
public class CopilotProxyController : ControllerBase
{
    private readonly HttpClient _httpClient;
    private readonly string _apiBaseUrl;
    private readonly ILogger<CopilotProxyController> _logger;

    public CopilotProxyController(HttpClient httpClient, IConfiguration configuration, ILogger<CopilotProxyController> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        // In Docker environment, Api:BaseUrl is set to http://api:8080.
        // For local development, falls back to http://localhost:5000.
        _apiBaseUrl = configuration["Api:BaseUrl"] ?? "http://localhost:5000";
    }

    [HttpPost("chat")]
    public async Task<IActionResult> ProxyChat([FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        var targetUrl = $"{_apiBaseUrl.TrimEnd('/')}/api/copilot/chat";
        _logger.LogInformation("Proxying Copilot chat request to backend API: {TargetUrl}", targetUrl);

        try
        {
            var content = new StringContent(body.GetRawText(), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(targetUrl, content, cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            return StatusCode((int)response.StatusCode, responseContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while proxying chat request to {TargetUrl}", targetUrl);
            return StatusCode(503, new
            {
                error = "ERP AI API service is currently unavailable or unreachable.",
                details = ex.Message,
                targetUrl = targetUrl
            });
        }
    }
}
