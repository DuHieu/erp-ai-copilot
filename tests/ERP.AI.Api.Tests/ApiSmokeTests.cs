using System.Net;
using System.Net.Http.Json;
using ERP.AI.Core.Interfaces;
using ERP.AI.Knowledge.Dtos;
using ERP.AI.Knowledge.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ERP.AI.Api.Tests;

public class ApiSmokeTests : IClassFixture<ApiApplicationFactory>
{
    private readonly ApiApplicationFactory _factory;

    public ApiSmokeTests(ApiApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Health_ReturnsHealthyJson()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        var json = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        json.Should().ContainKey("status");
    }

    [Fact]
    public async Task Readiness_ReturnsUnavailable_WhenExternalDependenciesAreDown()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/health/ready");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Unhealthy");
        body.Should().Contain("embeddingService");
        body.Should().Contain("qdrant");
    }

    [Fact]
    public async Task CopilotChat_RejectsWriteRequestsWithoutCallingLlm()
    {
        using var client = _factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/copilot/chat", new { message = "create invoice for MAEDA" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("read-only ERP queries");
    }

    [Fact]
    public async Task KnowledgeAsk_NoEvidence_DoesNotCallLlm()
    {
        CountingLlmProvider.Reset();
        using var client = _factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/knowledge/ask", new { question = "How do we repair a motorcycle engine?" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("\"noEvidence\":true");
        CountingLlmProvider.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Health_RemainsPublic_WhenApiKeyIsRequired()
    {
        using var app = _factory.WithApiKeyRequired("test-api-key");
        using var client = app.CreateClient();

        using var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ApiEndpoint_ReturnsUnauthorized_WhenApiKeyIsRequiredAndMissing()
    {
        using var app = _factory.WithApiKeyRequired("test-api-key");
        using var client = app.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/copilot/chat", new { message = "create invoice for MAEDA" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("API key");
    }

    [Fact]
    public async Task ApiEndpoint_AllowsRequest_WhenApiKeyIsValid()
    {
        using var app = _factory.WithApiKeyRequired("test-api-key");
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", "test-api-key");

        using var response = await client.PostAsJsonAsync("/api/copilot/chat", new { message = "create invoice for MAEDA" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("read-only ERP queries");
    }
}

public sealed class ApiApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"erp-ai-api-tests-{Guid.NewGuid():N}.db");
    private readonly Dictionary<string, string?> _originalEnvironment = new();

    public ApiApplicationFactory()
    {
        SetEnvironment("ConnectionStrings__DefaultConnection", $"Data Source={_dbPath}");
        SetEnvironment("AI__Provider", "Ollama");
        SetEnvironment("AI__Endpoint", "http://127.0.0.1:1");
        SetEnvironment("AI__Model", "qwen3");
        SetEnvironment("Embedding__Endpoint", "http://127.0.0.1:1");
        SetEnvironment("VectorStore__Endpoint", "http://127.0.0.1:1");
        SetEnvironment("Rag__MinimumScore", "0.35");
        SetEnvironment("Rag__TopK", "5");
        SetEnvironment("Logging__LogLevel__Microsoft.EntityFrameworkCore.Database.Command", "Warning");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = $"Data Source={_dbPath}",
                ["AI:Provider"] = "Ollama",
                ["AI:Endpoint"] = "http://127.0.0.1:1",
                ["AI:Model"] = "qwen3",
                ["Embedding:Endpoint"] = "http://127.0.0.1:1",
                ["VectorStore:Endpoint"] = "http://127.0.0.1:1",
                ["Rag:MinimumScore"] = "0.35",
                ["Rag:TopK"] = "5"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IKnowledgeSearchService>();
            services.RemoveAll<ILlmProvider>();
            services.AddScoped<IKnowledgeSearchService, EmptyKnowledgeSearchService>();
            services.AddSingleton<ILlmProvider, CountingLlmProvider>();
        });
    }

    public WebApplicationFactory<Program> WithApiKeyRequired(string apiKey)
        => WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Security:RequireApiKey"] = "true",
                    ["Security:ApiKey"] = apiKey,
                    ["Security:ApiKeyUserId"] = "integration-test-user"
                });
            });
        });

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        foreach (var (key, value) in _originalEnvironment)
        {
            Environment.SetEnvironmentVariable(key, value);
        }

        TryDelete(_dbPath);
        TryDelete($"{_dbPath}-shm");
        TryDelete($"{_dbPath}-wal");
    }

    private void SetEnvironment(string key, string value)
    {
        _originalEnvironment[key] = Environment.GetEnvironmentVariable(key);
        Environment.SetEnvironmentVariable(key, value);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best-effort cleanup only; test assertions do not depend on filesystem deletion.
        }
    }
}

internal sealed class EmptyKnowledgeSearchService : IKnowledgeSearchService
{
    public Task<SemanticSearchResponse> SearchAsync(SemanticSearchRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(new SemanticSearchResponse(request.Query, Array.Empty<SemanticSearchResult>(), 1, 0));
}

internal sealed class CountingLlmProvider : ILlmProvider
{
    private static int _callCount;

    public static int CallCount => _callCount;

    public static void Reset()
    {
        Interlocked.Exchange(ref _callCount, 0);
    }

    public Task<LlmChatResponse> ChatAsync(LlmChatRequest request, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _callCount);
        return Task.FromResult(new LlmChatResponse { Content = "This test LLM should not be called for no-evidence RAG." });
    }
}
