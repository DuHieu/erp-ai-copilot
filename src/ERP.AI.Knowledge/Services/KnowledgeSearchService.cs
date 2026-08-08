using System.Diagnostics;
using ERP.AI.Knowledge.Dtos;
using ERP.AI.Knowledge.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ERP.AI.Knowledge.Services;

public class KnowledgeSearchService : IKnowledgeSearchService
{
    private readonly IEmbeddingService _embeddingService;
    private readonly IKnowledgeVectorStore _vectorStore;
    private readonly ILogger<KnowledgeSearchService> _logger;
    private readonly int _defaultTopK;
    private readonly int _maxTopK;
    private readonly double _defaultMinScore;

    public KnowledgeSearchService(
        IEmbeddingService embeddingService,
        IKnowledgeVectorStore vectorStore,
        IConfiguration configuration,
        ILogger<KnowledgeSearchService> logger)
    {
        _embeddingService = embeddingService;
        _vectorStore = vectorStore;
        _logger = logger;

        _defaultTopK = int.TryParse(configuration["KnowledgeSearch:DefaultTopK"], out var tk) ? tk : 5;
        _maxTopK = int.TryParse(configuration["KnowledgeSearch:MaximumTopK"], out var mtk) ? mtk : 20;
        _defaultMinScore = double.TryParse(configuration["KnowledgeSearch:MinimumScore"], out var ms) ? ms : 0.35;
    }

    public async Task<SemanticSearchResponse> SearchAsync(SemanticSearchRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            throw new ArgumentException("Search query cannot be empty or whitespace.", nameof(request.Query));
        }

        var trimmedQuery = request.Query.Trim();
        if (trimmedQuery.Length > 2000)
        {
            trimmedQuery = trimmedQuery.Substring(0, 2000);
        }

        int topK = request.TopK > 0 ? Math.Min(request.TopK, _maxTopK) : _defaultTopK;
        double minScore = request.MinimumScore > 0 ? request.MinimumScore : _defaultMinScore;

        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation("Executing semantic search for query '{Query}' (TopK: {TopK}, MinScore: {MinScore})", trimmedQuery, topK, minScore);

        var queryEmbedding = await _embeddingService.EmbedQueryAsync(trimmedQuery, cancellationToken);
        var results = await _vectorStore.SearchAsync(
            queryVector: queryEmbedding.Vector,
            topK: topK,
            minScore: minScore,
            category: request.Category,
            language: request.Language,
            documentId: request.DocumentId,
            cancellationToken: cancellationToken
        );

        stopwatch.Stop();

        _logger.LogInformation("Semantic search completed in {DurationMs} ms. Found {Count} matching chunks.", stopwatch.ElapsedMilliseconds, results.Count);

        return new SemanticSearchResponse(
            Query: trimmedQuery,
            Results: results,
            DurationMs: stopwatch.ElapsedMilliseconds,
            TotalCandidates: results.Count
        );
    }
}
