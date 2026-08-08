using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace ERP.AI.Web.Controllers;

[ApiController]
[Route("api/knowledge")]
public class KnowledgeProxyController : ControllerBase
{
    private readonly HttpClient _httpClient;
    private readonly string _apiBaseUrl;
    private readonly ILogger<KnowledgeProxyController> _logger;

    public KnowledgeProxyController(HttpClient httpClient, IConfiguration configuration, ILogger<KnowledgeProxyController> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiBaseUrl = configuration["Api:BaseUrl"] ?? "http://localhost:5000";
    }

    [HttpGet("{**path}")]
    public async Task<IActionResult> ProxyGet(string? path, CancellationToken cancellationToken)
    {
        var queryString = Request.QueryString.HasValue ? Request.QueryString.Value : string.Empty;
        var targetUrl = $"{_apiBaseUrl.TrimEnd('/')}/api/knowledge/{path}{queryString}";
        _logger.LogInformation("Proxying Knowledge GET request to: {TargetUrl}", targetUrl);

        try
        {
            var response = await _httpClient.GetAsync(targetUrl, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            return StatusCode((int)response.StatusCode, content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed proxying Knowledge GET to {TargetUrl}", targetUrl);
            return StatusCode(503, new { error = "ERP AI API service is unavailable.", details = ex.Message });
        }
    }

    [HttpPost("{**path}")]
    public async Task<IActionResult> ProxyPost(string? path, CancellationToken cancellationToken)
    {
        var targetUrl = $"{_apiBaseUrl.TrimEnd('/')}/api/knowledge/{path}";
        _logger.LogInformation("Proxying Knowledge POST request to: {TargetUrl}", targetUrl);

        try
        {
            HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Post, targetUrl);

            if (Request.HasFormContentType)
            {
                var multipartContent = new MultipartFormDataContent();
                foreach (var formFile in Request.Form.Files)
                {
                    var fileStream = formFile.OpenReadStream();
                    var streamContent = new StreamContent(fileStream);
                    streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(formFile.ContentType);
                    multipartContent.Add(streamContent, formFile.Name, formFile.FileName);
                }

                foreach (var field in Request.Form)
                {
                    if (field.Key != "file")
                    {
                        multipartContent.Add(new StringContent(field.Value.ToString()), field.Key);
                    }
                }
                requestMessage.Content = multipartContent;
            }
            else
            {
                using var reader = new StreamReader(Request.Body, Encoding.UTF8);
                var rawBody = await reader.ReadToEndAsync(cancellationToken);
                requestMessage.Content = new StringContent(rawBody, Encoding.UTF8, Request.ContentType ?? "application/json");
            }

            var response = await _httpClient.SendAsync(requestMessage, cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            return StatusCode((int)response.StatusCode, responseContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed proxying Knowledge POST to {TargetUrl}", targetUrl);
            return StatusCode(503, new { error = "ERP AI API service is unavailable.", details = ex.Message });
        }
    }

    [HttpDelete("{**path}")]
    public async Task<IActionResult> ProxyDelete(string? path, CancellationToken cancellationToken)
    {
        var targetUrl = $"{_apiBaseUrl.TrimEnd('/')}/api/knowledge/{path}";
        _logger.LogInformation("Proxying Knowledge DELETE request to: {TargetUrl}", targetUrl);

        try
        {
            var response = await _httpClient.DeleteAsync(targetUrl, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            return StatusCode((int)response.StatusCode, content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed proxying Knowledge DELETE to {TargetUrl}", targetUrl);
            return StatusCode(503, new { error = "ERP AI API service is unavailable.", details = ex.Message });
        }
    }
}
