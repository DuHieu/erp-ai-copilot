using System.Net;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ERP.AI.Web.Tests;

public class ProxyControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ProxyControllerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CopilotProxy_PreservesJsonContentType()
    {
        var handler = new CapturingHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"answer":"ok"}""", Encoding.UTF8, "application/json")
            });

        using var app = BuildFactory(handler);
        using var client = app.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/copilot/chat", new { message = "hello" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Be("""{"answer":"ok"}""");
    }

    [Fact]
    public async Task KnowledgeProxy_ForwardsGetPathAndQuery()
    {
        var handler = new CapturingHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"items":[]}""", Encoding.UTF8, "application/json")
            });

        using var app = BuildFactory(handler);
        using var client = app.CreateClient();

        using var response = await client.GetAsync("/api/knowledge/documents?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
        handler.Requests.Should().ContainSingle();
        handler.Requests[0].RequestUri?.PathAndQuery.Should().Be("/api/knowledge/documents?page=1&pageSize=20");
    }

    private WebApplicationFactory<Program> BuildFactory(CapturingHandler handler)
        => _factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IHttpClientFactory>();
                services.AddSingleton<IHttpClientFactory>(new StubHttpClientFactory(handler));
            });
        });
}

internal sealed class StubHttpClientFactory : IHttpClientFactory
{
    private readonly HttpMessageHandler _handler;

    public StubHttpClientFactory(HttpMessageHandler handler)
    {
        _handler = handler;
    }

    public HttpClient CreateClient(string name)
        => new(_handler, disposeHandler: false)
        {
            BaseAddress = new Uri("http://api.test")
        };
}

internal sealed class CapturingHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

    public CapturingHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
    {
        _responseFactory = responseFactory;
    }

    public List<HttpRequestMessage> Requests { get; } = new();

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(CloneRequest(request));
        return Task.FromResult(_responseFactory(request));
    }

    private static HttpRequestMessage CloneRequest(HttpRequestMessage request)
        => new(request.Method, request.RequestUri);
}
