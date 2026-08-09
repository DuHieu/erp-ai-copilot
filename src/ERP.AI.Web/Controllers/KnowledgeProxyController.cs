using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace ERP.AI.Web.Controllers;

[ApiController]
[Route("api/knowledge")]
public class KnowledgeProxyController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<KnowledgeProxyController> _logger;

    public KnowledgeProxyController(IHttpClientFactory httpClientFactory, ILogger<KnowledgeProxyController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    [HttpGet("{**path}")]
    public async Task<IActionResult> ProxyGet(string? path, CancellationToken cancellationToken)
    {
        var queryString = Request.QueryString.HasValue ? Request.QueryString.Value : string.Empty;
        var targetPath = $"/api/knowledge/{path}{queryString}";
        _logger.LogInformation("Proxying Knowledge GET request to: {TargetPath}", targetPath);

        try
        {
            var httpClient = _httpClientFactory.CreateClient("ErpApi");
            var response = await httpClient.GetAsync(targetPath, cancellationToken);
            return await ProxyResponseResult.FromAsync(response, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed proxying Knowledge GET to {TargetPath}", targetPath);
            return StatusCode(503, new { error = "ERP AI API service is unavailable.", details = ex.Message });
        }
    }

    [HttpPost("{**path}")]
    public async Task<IActionResult> ProxyPost(string? path, CancellationToken cancellationToken)
    {
        var targetPath = $"/api/knowledge/{path}";
        _logger.LogInformation("Proxying Knowledge POST request to: {TargetPath}", targetPath);

        try
        {
            var httpClient = _httpClientFactory.CreateClient("ErpApi");
            HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Post, targetPath);

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

            var response = await httpClient.SendAsync(requestMessage, cancellationToken);
            return await ProxyResponseResult.FromAsync(response, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed proxying Knowledge POST to {TargetPath}", targetPath);
            return StatusCode(503, new { error = "ERP AI API service is unavailable.", details = ex.Message });
        }
    }

    [HttpDelete("{**path}")]
    public async Task<IActionResult> ProxyDelete(string? path, CancellationToken cancellationToken)
    {
        var targetPath = $"/api/knowledge/{path}";
        _logger.LogInformation("Proxying Knowledge DELETE request to: {TargetPath}", targetPath);

        try
        {
            var httpClient = _httpClientFactory.CreateClient("ErpApi");
            var response = await httpClient.DeleteAsync(targetPath, cancellationToken);
            return await ProxyResponseResult.FromAsync(response, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed proxying Knowledge DELETE to {TargetPath}", targetPath);
            return StatusCode(503, new { error = "ERP AI API service is unavailable.", details = ex.Message });
        }
    }
}
