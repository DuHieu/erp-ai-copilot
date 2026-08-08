using ERP.AI.Knowledge.Dtos;
using ERP.AI.Knowledge.Entities;
using ERP.AI.Knowledge.Enums;
using ERP.AI.Knowledge.Interfaces;
using ERP.AI.Knowledge.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ERP.AI.Knowledge.Tests;

public class KnowledgeSemanticSearchTests
{
    [Fact]
    public async Task SearchAsync_WithValidQuery_ReturnsRankedResultsAboveMinScore()
    {
        // Arrange
        var fakeEmbeddingService = new FakeEmbeddingService();
        var fakeVectorStore = new FakeVectorStore();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            { "KnowledgeSearch:DefaultTopK", "5" },
            { "KnowledgeSearch:MinimumScore", "0.35" }
        }).Build();

        var searchService = new KnowledgeSearchService(
            fakeEmbeddingService,
            fakeVectorStore,
            config,
            NullLogger<KnowledgeSearchService>.Instance
        );

        var request = new SemanticSearchRequest(
            Query: "Khi invoice quá hạn 14 ngày thì xử lý thế nào?",
            TopK: 5,
            MinimumScore: 0.35,
            Category: "Finance"
        );

        // Act
        var response = await searchService.SearchAsync(request);

        // Assert
        Assert.NotNull(response);
        Assert.Equal("Khi invoice quá hạn 14 ngày thì xử lý thế nào?", response.Query);
        Assert.NotEmpty(response.Results);
        Assert.All(response.Results, r => Assert.True(r.Score >= 0.35));
        Assert.Equal(1, response.Results[0].Rank);
    }

    [Fact]
    public async Task SearchAsync_WithEmptyQuery_ThrowsArgumentException()
    {
        // Arrange
        var fakeEmbeddingService = new FakeEmbeddingService();
        var fakeVectorStore = new FakeVectorStore();
        var config = new ConfigurationBuilder().Build();
        var searchService = new KnowledgeSearchService(fakeEmbeddingService, fakeVectorStore, config, NullLogger<KnowledgeSearchService>.Instance);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => searchService.SearchAsync(new SemanticSearchRequest(Query: "   ")));
    }

    [Fact]
    public async Task SearchAsync_ClampsTopK_ToMaximumLimit()
    {
        // Arrange
        var fakeEmbeddingService = new FakeEmbeddingService();
        var fakeVectorStore = new FakeVectorStore();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            { "KnowledgeSearch:MaximumTopK", "20" }
        }).Build();
        var searchService = new KnowledgeSearchService(fakeEmbeddingService, fakeVectorStore, config, NullLogger<KnowledgeSearchService>.Instance);

        var request = new SemanticSearchRequest(Query: "Overdue payment", TopK: 100);

        // Act
        var response = await searchService.SearchAsync(request);

        // Assert
        Assert.True(fakeVectorStore.LastTopK <= 20);
    }

    [Fact]
    public void KnowledgeDocument_VectorProperties_DefaultValuesCorrect()
    {
        // Arrange & Act
        var doc = new KnowledgeDocument();

        // Assert
        Assert.Equal(EmbeddingStatus.NotIndexed, doc.EmbeddingStatus);
        Assert.Null(doc.EmbeddingModel);
        Assert.Null(doc.EmbeddingError);
        Assert.Equal(0, doc.EmbeddedChunkCount);
        Assert.Null(doc.IndexedAt);
    }

    // --- Fake Test Mocks ---

    private class FakeEmbeddingService : IEmbeddingService
    {
        public Task<EmbeddingResult> EmbedQueryAsync(string text, CancellationToken cancellationToken = default)
        {
            var fakeVector = new float[] { 0.1f, 0.2f, 0.3f, 0.4f };
            return Task.FromResult(new EmbeddingResult(fakeVector, 4, "BAAI/bge-m3"));
        }

        public Task<IReadOnlyList<EmbeddingResult>> EmbedDocumentsAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
        {
            var list = texts.Select(t => new EmbeddingResult(new float[] { 0.1f, 0.2f, 0.3f, 0.4f }, 4, "BAAI/bge-m3")).ToList();
            return Task.FromResult<IReadOnlyList<EmbeddingResult>>(list);
        }
    }

    private class FakeVectorStore : IKnowledgeVectorStore
    {
        public int LastTopK { get; private set; }

        public Task EnsureCollectionAsync(int dimension, string distanceMetric = "Cosine", CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task UpsertAsync(IReadOnlyList<VectorPoint> points, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task DeleteDocumentAsync(string documentId, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<SemanticSearchResult>> SearchAsync(
            IReadOnlyList<float> queryVector,
            int topK,
            double minScore,
            string? category = null,
            string? language = null,
            string? documentId = null,
            CancellationToken cancellationToken = default)
        {
            LastTopK = topK;

            var mockResults = new List<SemanticSearchResult>();
            if (minScore <= 0.85)
            {
                mockResults.Add(new SemanticSearchResult(
                    Rank: 1,
                    Score: 0.872,
                    ChunkId: "chunk-001",
                    DocumentId: "doc-001",
                    DocumentTitle: "AR Collection Procedure",
                    FileName: "finance-procedure.txt",
                    Category: category ?? "Finance",
                    SectionTitle: "Overdue Collection",
                    HeadingPath: "Accounts Receivable > Escalation",
                    StartPage: 4,
                    EndPage: 4,
                    Content: "If an invoice is overdue by 14 days, notify the Finance Manager immediately.",
                    Source: "Finance SOP",
                    Version: "3.2",
                    Language: "English"
                ));
            }

            return Task.FromResult<IReadOnlyList<SemanticSearchResult>>(mockResults);
        }
    }
}
