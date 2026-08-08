using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ERP.AI.Knowledge.Dtos;
using ERP.AI.Knowledge.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ERP.AI.Knowledge.Services;

public class QdrantKnowledgeVectorStore : IKnowledgeVectorStore
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<QdrantKnowledgeVectorStore> _logger;
    private readonly string _endpoint;
    private readonly string _collection;

    public QdrantKnowledgeVectorStore(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<QdrantKnowledgeVectorStore> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        var baseUrl = configuration["VectorStore:Endpoint"] ?? "http://localhost:6333";
        _endpoint = baseUrl.TrimEnd('/');

        _collection = configuration["VectorStore:Collection"] ?? "erp_knowledge_chunks";
    }

    public async Task EnsureCollectionAsync(int dimension, string distanceMetric = "Cosine", CancellationToken cancellationToken = default)
    {
        var collectionUrl = $"{_endpoint}/collections/{_collection}";
        
        try
        {
            var getResp = await _httpClient.GetAsync(collectionUrl, cancellationToken);
            if (getResp.IsSuccessStatusCode)
            {
                _logger.LogInformation("Qdrant collection '{Collection}' already exists.", _collection);
                return;
            }

            _logger.LogInformation("Creating Qdrant collection '{Collection}' (Dimension: {Dimension}, Distance: {Distance})...", _collection, dimension, distanceMetric);

            var createPayload = new
            {
                vectors = new
                {
                    size = dimension,
                    distance = distanceMetric
                }
            };

            var putResp = await _httpClient.PutAsJsonAsync(collectionUrl, createPayload, cancellationToken);
            if (!putResp.IsSuccessStatusCode)
            {
                var errText = await putResp.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Failed to create Qdrant collection '{Collection}': {ErrorText}", _collection, errText);
                throw new InvalidOperationException($"Qdrant error ({putResp.StatusCode}): {errText}");
            }

            _logger.LogInformation("Qdrant collection '{Collection}' created successfully.", _collection);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogError(ex, "Failed to connect to Qdrant at {Endpoint}", _endpoint);
            throw new InvalidOperationException($"Qdrant service unreachable at {_endpoint}. Ensure qdrant service is running.", ex);
        }
    }

    public async Task UpsertAsync(IReadOnlyList<VectorPoint> points, CancellationToken cancellationToken = default)
    {
        if (points == null || points.Count == 0) return;

        var upsertUrl = $"{_endpoint}/collections/{_collection}/points";
        var pointPayloads = points.Select(p => new
        {
            id = FormatUuid(p.PointId),
            vector = p.Vector,
            payload = p.Payload
        }).ToList();

        try
        {
            var resp = await _httpClient.PutAsJsonAsync(upsertUrl, new { points = pointPayloads }, cancellationToken);
            if (!resp.IsSuccessStatusCode)
            {
                var errText = await resp.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Qdrant point upsert failed: {ErrorText}", errText);
                throw new InvalidOperationException($"Qdrant upsert error ({resp.StatusCode}): {errText}");
            }
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogError(ex, "Qdrant connection error during point upsert to {Endpoint}", _endpoint);
            throw new InvalidOperationException($"Qdrant service unreachable at {_endpoint}.", ex);
        }
    }

    public async Task DeleteDocumentAsync(string documentId, CancellationToken cancellationToken = default)
    {
        var deleteUrl = $"{_endpoint}/collections/{_collection}/points/delete";
        var deletePayload = new
        {
            filter = new
            {
                must = new object[]
                {
                    new { key = "documentId", match = new { value = documentId } }
                }
            }
        };

        try
        {
            var resp = await _httpClient.PostAsJsonAsync(deleteUrl, deletePayload, cancellationToken);
            if (!resp.IsSuccessStatusCode && resp.StatusCode != System.Net.HttpStatusCode.NotFound)
            {
                var errText = await resp.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Qdrant document vector delete warning ({StatusCode}): {ErrorText}", resp.StatusCode, errText);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete Qdrant points for document {DocumentId}", documentId);
        }
    }

    public async Task<IReadOnlyList<SemanticSearchResult>> SearchAsync(
        IReadOnlyList<float> queryVector,
        int topK,
        double minScore,
        string? category = null,
        string? language = null,
        string? documentId = null,
        CancellationToken cancellationToken = default)
    {
        var searchUrl = $"{_endpoint}/collections/{_collection}/points/search";

        var filterMust = new List<object>();
        if (!string.IsNullOrWhiteSpace(category))
        {
            filterMust.Add(new { key = "category", match = new { value = category } });
        }
        if (!string.IsNullOrWhiteSpace(language) && !language.Equals("Auto", StringComparison.OrdinalIgnoreCase))
        {
            filterMust.Add(new { key = "language", match = new { value = language } });
        }
        if (!string.IsNullOrWhiteSpace(documentId))
        {
            filterMust.Add(new { key = "documentId", match = new { value = documentId } });
        }

        object? filterObj = filterMust.Count > 0 ? new { must = filterMust } : null;

        var searchPayload = new
        {
            vector = queryVector,
            limit = topK,
            score_threshold = minScore,
            with_payload = true,
            filter = filterObj
        };

        try
        {
            var resp = await _httpClient.PostAsJsonAsync(searchUrl, searchPayload, cancellationToken);
            if (!resp.IsSuccessStatusCode)
            {
                var errText = await resp.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Qdrant vector search failed: {ErrorText}", errText);
                throw new InvalidOperationException($"Qdrant search error ({resp.StatusCode}): {errText}");
            }

            var json = await resp.Content.ReadFromJsonAsync<QdrantSearchResponseDto>(cancellationToken: cancellationToken);
            if (json == null || json.Result == null)
            {
                return Array.Empty<SemanticSearchResult>();
            }

            var results = new List<SemanticSearchResult>();
            int rank = 1;

            foreach (var point in json.Result)
            {
                var p = point.Payload ?? new Dictionary<string, JsonElement>();
                results.Add(new SemanticSearchResult(
                    Rank: rank++,
                    Score: Math.Round(point.Score, 4),
                    ChunkId: GetPayloadString(p, "chunkId"),
                    DocumentId: GetPayloadString(p, "documentId"),
                    DocumentTitle: GetPayloadString(p, "documentTitle"),
                    FileName: GetPayloadString(p, "fileName"),
                    Category: GetPayloadString(p, "category"),
                    SectionTitle: GetPayloadString(p, "sectionTitle"),
                    HeadingPath: GetPayloadString(p, "headingPath"),
                    StartPage: GetPayloadInt(p, "startPage"),
                    EndPage: GetPayloadInt(p, "endPage"),
                    Content: GetPayloadString(p, "content"),
                    Source: GetPayloadString(p, "source"),
                    Version: GetPayloadString(p, "version"),
                    Language: GetPayloadString(p, "language")
                ));
            }

            return results;
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogError(ex, "Qdrant connection error during search to {Endpoint}", _endpoint);
            throw new InvalidOperationException($"Qdrant service unreachable at {_endpoint}.", ex);
        }
    }

    private static string FormatUuid(string rawId)
    {
        if (Guid.TryParse(rawId, out var g))
        {
            return g.ToString("D");
        }
        return Guid.NewGuid().ToString("D");
    }

    private static string GetPayloadString(Dictionary<string, JsonElement> p, string key)
    {
        if (p.TryGetValue(key, out var val) && val.ValueKind == JsonValueKind.String)
        {
            return val.GetString() ?? string.Empty;
        }
        return string.Empty;
    }

    private static int? GetPayloadInt(Dictionary<string, JsonElement> p, string key)
    {
        if (p.TryGetValue(key, out var val) && val.ValueKind == JsonValueKind.Number && val.TryGetInt32(out var i))
        {
            return i;
        }
        return null;
    }

    private class QdrantSearchResponseDto
    {
        [JsonPropertyName("result")]
        public List<QdrantSearchResultPointDto>? Result { get; set; }
    }

    private class QdrantSearchResultPointDto
    {
        [JsonPropertyName("id")]
        public object? Id { get; set; }

        [JsonPropertyName("score")]
        public double Score { get; set; }

        [JsonPropertyName("payload")]
        public Dictionary<string, JsonElement>? Payload { get; set; }
    }
}
