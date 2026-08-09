namespace ERP.AI.Web;

public sealed class ApiKeyProxyHeaderHandler : DelegatingHandler
{
    private readonly IConfiguration _configuration;

    public ApiKeyProxyHeaderHandler(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var apiKey = _configuration["Api:ApiKey"];
        if (!string.IsNullOrWhiteSpace(apiKey) && !request.Headers.Contains("X-API-Key"))
        {
            request.Headers.Add("X-API-Key", apiKey);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
