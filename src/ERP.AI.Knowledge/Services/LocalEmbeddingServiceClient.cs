using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ERP.AI.Knowledge.Dtos;
using ERP.AI.Knowledge.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ERP.AI.Knowledge.Services;

public class LocalEmbeddingServiceClient : IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<LocalEmbeddingServiceClient> _logger;
    private readonly string _endpoint;
    private readonly int _batchSize;

    public LocalEmbeddingServiceClient(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<LocalEmbeddingServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        var baseUrl = configuration["Embedding:Endpoint"] ?? "http://localhost:8010";
        _endpoint = $"{baseUrl.TrimEnd('/')}/embed";

        _batchSize = int.TryParse(configuration["Embedding:BatchSize"], out var bs) ? bs : 16;
    }

    public async Task<EmbeddingResult> EmbedQueryAsync(string text, CancellationToken cancellationToken = default)
    {
        var results = await EmbedDocumentsAsync(new[] { text }, cancellationToken);
        return results.First();
    }

    public async Task<IReadOnlyList<EmbeddingResult>> EmbedDocumentsAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
    {
        if (texts == null || texts.Count == 0)
        {
            return Array.Empty<EmbeddingResult>();
        }

        var results = new List<EmbeddingResult>();

        for (int i = 0; i < texts.Count; i += _batchSize)
        {
            var batch = texts.Skip(i).Take(_batchSize).ToList();
            _logger.LogInformation("Sending embedding batch request ({Current}/{Total}) to {Endpoint}", i + batch.Count, texts.Count, _endpoint);

            try
            {
                var response = await _httpClient.PostAsJsonAsync(_endpoint, new { texts = batch }, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    var errText = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError("Embedding service returned HTTP status {StatusCode}: {ErrorText}", response.StatusCode, errText);
                    throw new InvalidOperationException($"Embedding service error ({response.StatusCode}): {errText}");
                }

                var json = await response.Content.ReadFromJsonAsync<EmbedResponseDto>(cancellationToken: cancellationToken);
                if (json == null || json.Embeddings == null || json.Embeddings.Count != batch.Count)
                {
                    throw new InvalidOperationException("Mismatched vector counts returned by embedding service.");
                }

                for (int j = 0; j < batch.Count; j++)
                {
                    var floatVector = json.Embeddings[j].Select(d => (float)d).ToList();
                    results.Add(new EmbeddingResult(floatVector, json.Dimension, json.Model));
                }
            }
            catch (Exception ex) when (ex is not InvalidOperationException)
            {
                _logger.LogError(ex, "Failed to reach embedding service at {Endpoint}", _endpoint);
                throw new InvalidOperationException($"Embedding service unreachable at {_endpoint}. Ensure embedding-service is running.", ex);
            }
        }

        return results;
    }

    private class EmbedResponseDto
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("dimension")]
        public int Dimension { get; set; }

        [JsonPropertyName("embeddings")]
        public List<List<double>> Embeddings { get; set; } = new();
    }
}
