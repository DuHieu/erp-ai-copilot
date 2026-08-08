using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ERP.AI.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;

namespace ERP.AI.Api.Controllers;

[ApiController]
public class HealthCheckController : ControllerBase
{
    private readonly ErpDbContext _dbContext;
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<HealthCheckController> _logger;

    public HealthCheckController(
        ErpDbContext dbContext,
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<HealthCheckController> logger)
    {
        _dbContext = dbContext;
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Liveness probe: Verifies that the API application process is running.
    /// </summary>
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { status = "Healthy", timestamp = DateTime.UtcNow });
    }

    /// <summary>
    /// Readiness probe: Verifies SQLite DB, Ollama endpoint, Embedding service, and Qdrant vector store.
    /// </summary>
    [HttpGet("health/ready")]
    public async Task<IActionResult> HealthReady(CancellationToken cancellationToken)
    {
        var dbStatus = "Unhealthy";
        var ollamaStatus = "Unhealthy";
        var embeddingStatus = "Unhealthy";
        var qdrantStatus = "Unhealthy";

        var configuredModel = _configuration["AI:Model"] ?? "qwen3";
        var ollamaEndpoint = _configuration["AI:Endpoint"] ?? "http://localhost:11434";
        var embeddingEndpoint = _configuration["Embedding:Endpoint"] ?? "http://localhost:8010";
        var qdrantEndpoint = _configuration["VectorStore:Endpoint"] ?? "http://localhost:6333";

        bool isOverallHealthy = true;

        // 1. Check SQLite Database Connectivity
        try
        {
            bool canConnect = await _dbContext.Database.CanConnectAsync(cancellationToken);
            if (canConnect)
            {
                dbStatus = "Healthy";
            }
            else
            {
                isOverallHealthy = false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Health check database connection failed.");
            dbStatus = $"Unhealthy ({ex.Message})";
            isOverallHealthy = false;
        }

        // 2. Check Ollama API & Configured Model Tag
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            var tagsUrl = $"{ollamaEndpoint.TrimEnd('/')}/api/tags";
            var response = await _httpClient.GetAsync(tagsUrl, cts.Token);
            if (response.IsSuccessStatusCode)
            {
                var jsonNode = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken: cts.Token);
                var modelsArray = jsonNode?["models"]?.AsArray();

                bool modelExists = false;
                if (modelsArray != null)
                {
                    foreach (var m in modelsArray)
                    {
                        var name = m?["name"]?.ToString() ?? "";
                        if (name.StartsWith(configuredModel, StringComparison.OrdinalIgnoreCase))
                        {
                            modelExists = true;
                            break;
                        }
                    }
                }

                if (modelExists)
                {
                    ollamaStatus = "Healthy";
                }
                else
                {
                    ollamaStatus = $"Degraded (Model '{configuredModel}' not found in Ollama list)";
                    isOverallHealthy = false;
                }
            }
            else
            {
                ollamaStatus = $"Unhealthy (Status {response.StatusCode})";
                isOverallHealthy = false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Health check Ollama reachability failed.");
            ollamaStatus = "Unhealthy (Ollama endpoint unreachable)";
            isOverallHealthy = false;
        }

        // 3. Check Embedding Service Health
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(3));

            var url = $"{embeddingEndpoint.TrimEnd('/')}/health";
            var response = await _httpClient.GetAsync(url, cts.Token);
            if (response.IsSuccessStatusCode)
            {
                embeddingStatus = "Healthy";
            }
            else
            {
                embeddingStatus = $"Unhealthy ({response.StatusCode})";
                isOverallHealthy = false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Health check embedding service reachability failed.");
            embeddingStatus = "Unhealthy (Embedding endpoint unreachable)";
            isOverallHealthy = false;
        }

        // 4. Check Qdrant Health
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(3));

            var url = $"{qdrantEndpoint.TrimEnd('/')}/healthz";
            var response = await _httpClient.GetAsync(url, cts.Token);
            if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                qdrantStatus = "Healthy";
            }
            else
            {
                qdrantStatus = $"Unhealthy ({response.StatusCode})";
                isOverallHealthy = false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Health check Qdrant reachability failed.");
            qdrantStatus = "Unhealthy (Qdrant endpoint unreachable)";
            isOverallHealthy = false;
        }

        var responseBody = new
        {
            status = isOverallHealthy ? "Healthy" : "Unhealthy",
            timestamp = DateTime.UtcNow,
            checks = new
            {
                database = dbStatus,
                ollama = ollamaStatus,
                embeddingService = embeddingStatus,
                qdrant = qdrantStatus,
                model = configuredModel
            }
        };

        return isOverallHealthy ? Ok(responseBody) : StatusCode(503, responseBody);
    }
}
