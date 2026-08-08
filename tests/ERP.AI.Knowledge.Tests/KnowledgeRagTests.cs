using ERP.AI.Core.Interfaces;
using ERP.AI.Knowledge.Dtos;
using ERP.AI.Knowledge.Interfaces;
using ERP.AI.Knowledge.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ERP.AI.Knowledge.Tests;

/// <summary>
/// Unit tests for Phase 2.3 Grounded RAG Chat.
/// All tests are offline — no Ollama, Qdrant, or embedding service required.
/// Key acceptance: no LLM call occurs when retrieval evidence is insufficient.
/// </summary>
public class KnowledgeRagTests
{
    // -----------------------------------------------------------------------
    // Test 1: Relevant query calls LLM and returns grounded answer
    // -----------------------------------------------------------------------
    [Fact]
    public async Task RelevantQuery_CallsLlm_ReturnsGroundedAnswer()
    {
        // Arrange
        var (fakeLlm, fakeSearch, ragService) = BuildRagService(
            searchResults: new[] { MakeResult(score: 0.87, chunkId: "c1", docId: "doc1", docTitle: "AR Procedure") },
            llmAnswer: "Khi invoice quá hạn 14 ngày, cần thông báo Finance Manager. [1]"
        );

        // Act
        var response = await ragService.AskAsync(new RagChatRequest("Khi invoice quá hạn 14 ngày?"));

        // Assert
        Assert.True(response.Grounded);
        Assert.False(response.NoEvidence);
        Assert.Contains("[1]", response.Answer);
        Assert.Single(response.Sources);
        Assert.Equal(1, fakeLlm.CallCount);
        Assert.Equal("AR Procedure", response.Sources[0].DocumentTitle);
    }

    // -----------------------------------------------------------------------
    // Test 2: No evidence — LLM must NOT be called
    // -----------------------------------------------------------------------
    [Fact]
    public async Task NoEvidence_DoesNotCallLlm_ReturnsRefusal()
    {
        // Arrange
        var (fakeLlm, fakeSearch, ragService) = BuildRagService(searchResults: Array.Empty<SemanticSearchResult>());

        // Act
        var response = await ragService.AskAsync(new RagChatRequest("How to repair a motorcycle engine?"));

        // Assert
        Assert.False(response.Grounded);
        Assert.True(response.NoEvidence);
        Assert.Empty(response.Sources);
        Assert.Equal(0, fakeLlm.CallCount); // CRITICAL: LLM must not be called
        Assert.Equal(0, response.RetrievedChunkCount);
    }

    // -----------------------------------------------------------------------
    // Test 3: Low score below threshold triggers no-evidence (no LLM call)
    // -----------------------------------------------------------------------
    [Fact]
    public async Task LowScore_BelowThreshold_ReturnsNoEvidence()
    {
        // Arrange
        var (fakeLlm, _, ragService) = BuildRagService(
            searchResults: new[] { MakeResult(score: 0.20, chunkId: "c1", docId: "doc1") },
            minScore: 0.35
        );

        // Act
        var response = await ragService.AskAsync(new RagChatRequest("Some query"));

        // Assert
        Assert.True(response.NoEvidence);
        Assert.Equal(0, fakeLlm.CallCount);
    }

    // -----------------------------------------------------------------------
    // Test 4: Citation IDs are assigned correctly [1], [2]
    // -----------------------------------------------------------------------
    [Fact]
    public async Task ValidSources_CitationIdsAssignedCorrectly()
    {
        // Arrange
        var (_, _, ragService) = BuildRagService(
            searchResults: new[] {
                MakeResult(score: 0.85, chunkId: "c1", docId: "doc1", docTitle: "Finance Doc"),
                MakeResult(score: 0.75, chunkId: "c2", docId: "doc2", docTitle: "HR Policy")
            },
            llmAnswer: "Finance approval needed. [1] HR rules apply. [2]"
        );

        // Act
        var response = await ragService.AskAsync(new RagChatRequest("What are the rules?"));

        // Assert
        Assert.True(response.Grounded);
        Assert.Equal(2, response.Sources.Count);
        Assert.Equal(1, response.Sources[0].CitationId);
        Assert.Equal(2, response.Sources[1].CitationId);
        Assert.Equal("Finance Doc", response.Sources[0].DocumentTitle);
        Assert.Equal("HR Policy", response.Sources[1].DocumentTitle);
    }

    // -----------------------------------------------------------------------
    // Test 5: Invalid citation ID is sanitized from answer
    // -----------------------------------------------------------------------
    [Fact]
    public async Task InvalidCitationId_Sanitized_FromAnswer()
    {
        // Arrange: LLM returns [5] but only [1] and [2] exist
        var (_, _, ragService) = BuildRagService(
            searchResults: new[] {
                MakeResult(score: 0.85, chunkId: "c1", docId: "doc1"),
                MakeResult(score: 0.75, chunkId: "c2", docId: "doc2")
            },
            llmAnswer: "Answer from doc. [1] Also from doc. [5]"
        );

        // Act
        var response = await ragService.AskAsync(new RagChatRequest("Question?"));

        // Assert: [5] should be removed
        Assert.DoesNotContain("[5]", response.Answer);
        Assert.Contains("[1]", response.Answer);
    }

    // -----------------------------------------------------------------------
    // Test 6: Missing citations trigger one retry (max 1)
    // -----------------------------------------------------------------------
    [Fact]
    public async Task MissingCitation_TriggersOneRetry_MaxOne()
    {
        // Arrange: First call returns answer without citations (>80 chars to trigger retry),
        // retry answer contains [1] citation.
        var fakeLlm = new FakeRagLlmProvider(
            firstAnswer: "This is a detailed answer about the finance procedure without any citation reference from the retrieved document context.",
            retryAnswer: "This is a grounded answer about the finance procedure. [1]"
        );
        var ragService = BuildRagServiceWithFakes(
            searchResults: new[] { MakeResult(score: 0.85, chunkId: "c1", docId: "doc1") },
            fakeLlm: fakeLlm
        );

        // Act
        var response = await ragService.AskAsync(new RagChatRequest("Question?"));

        // Assert: exactly 2 LLM calls total (original + 1 retry)
        Assert.Equal(2, fakeLlm.CallCount);
    }

    // -----------------------------------------------------------------------
    // Test 7: Duplicate chunks are deduplicated
    // -----------------------------------------------------------------------
    [Fact]
    public void DuplicateChunks_Deduplicated_ByChunkId()
    {
        // Arrange
        var builder = new GroundingContextBuilder();
        var results = new[]
        {
            MakeResult(score: 0.90, chunkId: "chunk-1", docId: "doc1"),
            MakeResult(score: 0.88, chunkId: "chunk-1", docId: "doc1"), // Duplicate!
            MakeResult(score: 0.80, chunkId: "chunk-2", docId: "doc1"),
        };

        // Act
        var context = builder.Build(results, new GroundingContextOptions(MaxSources: 5));

        // Assert: only unique chunks
        Assert.Equal(2, context.UsedChunkCount);
        Assert.Equal(2, context.Sources.Count);
    }

    // -----------------------------------------------------------------------
    // Test 8: Context budget is enforced by character limit
    // -----------------------------------------------------------------------
    [Fact]
    public void ContextBudget_EnforcedByCharacterLimit()
    {
        // Arrange
        var builder = new GroundingContextBuilder();
        var bigContent = new string('X', 5000); // Large chunk
        var results = new[]
        {
            MakeResult(score: 0.90, chunkId: "c1", docId: "doc1", content: bigContent),
            MakeResult(score: 0.85, chunkId: "c2", docId: "doc2", content: bigContent),
            MakeResult(score: 0.80, chunkId: "c3", docId: "doc3", content: bigContent),
        };

        // Act: max context = 8000 chars, each chunk = 3500 chars max
        var context = builder.Build(results, new GroundingContextOptions(
            MaxSources: 5,
            MaxContextCharacters: 8000,
            MaxChunkCharacters: 3500
        ));

        // Assert: total formatted context respects budget
        Assert.True(context.FormattedContext.Length <= 9000, // Some overhead for headers
            $"Context exceeded budget: {context.FormattedContext.Length} chars");
    }

    // -----------------------------------------------------------------------
    // Test 9: Multiple sources cited correctly
    // -----------------------------------------------------------------------
    [Fact]
    public async Task MultipleSourcesCited_Correctly()
    {
        // Arrange
        var (_, _, ragService) = BuildRagService(
            searchResults: new[] {
                MakeResult(score: 0.85, chunkId: "c1", docId: "doc1", docTitle: "Finance"),
                MakeResult(score: 0.75, chunkId: "c2", docId: "doc2", docTitle: "HR")
            },
            llmAnswer: "Finance Director approves payments. [1] HR policy confirms. [2]\n\nSources:\n[1] Finance\n[2] HR"
        );

        // Act
        var response = await ragService.AskAsync(new RagChatRequest("Who approves?"));

        // Assert
        Assert.Equal(2, response.Sources.Count);
        Assert.Contains("[1]", response.Answer);
        Assert.Contains("[2]", response.Answer);
    }

    // -----------------------------------------------------------------------
    // Test 10: Conversation history limited to MaxConversationTurns
    // -----------------------------------------------------------------------
    [Fact]
    public void ConversationHistory_LimitedToMaxTurns()
    {
        // Arrange
        var store = new KnowledgeRagConversationStore();
        var convId = Guid.NewGuid().ToString();
        const int maxTurns = 3;

        // Act: add 5 turns
        for (int i = 0; i < 5; i++)
            store.AddTurn(convId, $"Q{i}", $"A{i}");

        var recent = store.GetRecentTurns(convId, maxTurns);

        // Assert: only last 3 returned
        Assert.Equal(maxTurns, recent.Count);
        Assert.Equal("Q2", recent[0].Question); // Q2, Q3, Q4 are most recent
    }

    // -----------------------------------------------------------------------
    // Test 11: Embedding failure propagates as exception
    // -----------------------------------------------------------------------
    [Fact]
    public async Task EmbeddingFailure_Propagates_AsException()
    {
        // Arrange
        var failingSearch = new FakeFailingSearchService(throwOnSearch: true);
        var ragService = BuildRagServiceWithFakes(
            searchResults: Array.Empty<SemanticSearchResult>(),
            fakeLlm: new FakeRagLlmProvider("answer"),
            fakeSearch: failingSearch
        );

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ragService.AskAsync(new RagChatRequest("Question?")));
    }

    // -----------------------------------------------------------------------
    // Test 12: LLM failure returns 503-style response with sources
    // -----------------------------------------------------------------------
    [Fact]
    public async Task LlmFailure_Returns_PartialResponse_WithSources()
    {
        // Arrange
        var failingLlm = new FakeFailingLlmProvider();
        var ragService = BuildRagServiceWithFakes(
            searchResults: new[] { MakeResult(score: 0.85, chunkId: "c1", docId: "doc1") },
            fakeLlm: failingLlm
        );

        // Act
        var response = await ragService.AskAsync(new RagChatRequest("Question?"));

        // Assert: sources returned even if LLM fails
        Assert.False(response.Grounded);
        Assert.NotEmpty(response.Sources);
        Assert.Contains("unavailable", response.Answer, StringComparison.OrdinalIgnoreCase);
    }

    // -----------------------------------------------------------------------
    // Test 13: GroundingContextBuilder assigns sequential citation IDs
    // -----------------------------------------------------------------------
    [Fact]
    public void GroundingContextBuilder_AssignsSequentialCitationIds()
    {
        // Arrange
        var builder = new GroundingContextBuilder();
        var results = new[]
        {
            MakeResult(score: 0.90, chunkId: "c1", docId: "doc1"),
            MakeResult(score: 0.80, chunkId: "c2", docId: "doc2"),
            MakeResult(score: 0.70, chunkId: "c3", docId: "doc3"),
        };

        // Act
        var context = builder.Build(results, new GroundingContextOptions(MaxSources: 5));

        // Assert
        Assert.Equal(3, context.Sources.Count);
        Assert.Equal(1, context.Sources[0].CitationId);
        Assert.Equal(2, context.Sources[1].CitationId);
        Assert.Equal(3, context.Sources[2].CitationId);
    }

    // -----------------------------------------------------------------------
    // Test 14: Context builder includes SOURCE [N] / END SOURCE [N] markers
    // -----------------------------------------------------------------------
    [Fact]
    public void GroundingContextBuilder_IncludesSourceMarkers()
    {
        // Arrange
        var builder = new GroundingContextBuilder();
        var results = new[] { MakeResult(score: 0.85, chunkId: "c1", docId: "doc1") };

        // Act
        var context = builder.Build(results, new GroundingContextOptions());

        // Assert: prompt injection protection markers present
        Assert.Contains("BEGIN SOURCE [1]", context.FormattedContext);
        Assert.Contains("END SOURCE [1]", context.FormattedContext);
    }

    // -----------------------------------------------------------------------
    // Test 15: CitationValidator detects unknown IDs
    // -----------------------------------------------------------------------
    [Fact]
    public void CitationValidator_DetectsUnknownIds()
    {
        // Arrange
        var validator = new CitationValidator();
        var sources = new[]
        {
            new SourceCitationDto(1, "doc1", "c1", "Title", "file.txt", null, null, null, null, null, 0.85, null)
        };

        // Act
        var result = validator.Validate("Answer [1] and also [5].", sources);

        // Assert
        Assert.True(result.HasUnknownIds);
        Assert.Contains(5, result.UnknownIds);
    }

    // -----------------------------------------------------------------------
    // Test 16: CitationValidator sanitizes invalid IDs
    // -----------------------------------------------------------------------
    [Fact]
    public void CitationValidator_Sanitizes_InvalidIds()
    {
        // Arrange
        var validator = new CitationValidator();
        var sources = new[]
        {
            new SourceCitationDto(1, "doc1", "c1", "Title", "file.txt", null, null, null, null, null, 0.85, null),
            new SourceCitationDto(2, "doc2", "c2", "Title2", "file2.txt", null, null, null, null, null, 0.75, null)
        };

        // Act
        var sanitized = validator.SanitizeInvalidCitationIds("Answer [1] and [3] and [2].", sources);

        // Assert
        Assert.Contains("[1]", sanitized);
        Assert.Contains("[2]", sanitized);
        Assert.DoesNotContain("[3]", sanitized);
    }

    // -----------------------------------------------------------------------
    // Test 17: Follow-up question still calls search service
    // -----------------------------------------------------------------------
    [Fact]
    public async Task FollowUpQuestion_StillCallsSearchService()
    {
        // Arrange
        var fakeSearch = new FakeCountingSearchService(
            new[] { MakeResult(score: 0.85, chunkId: "c1", docId: "doc1") }
        );
        var ragService = BuildRagServiceWithFakes(
            searchResults: Array.Empty<SemanticSearchResult>(),
            fakeLlm: new FakeRagLlmProvider("Answer. [1]"),
            fakeSearch: fakeSearch
        );

        var convId = Guid.NewGuid().ToString();

        // Act: two turns
        await ragService.AskAsync(new RagChatRequest("First question?", convId));
        await ragService.AskAsync(new RagChatRequest("Follow-up?", convId));

        // Assert: search called for each turn
        Assert.Equal(2, fakeSearch.CallCount);
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static SemanticSearchResult MakeResult(
        double score,
        string chunkId,
        string docId,
        string docTitle = "Test Document",
        string content = "Invoice overdue 14 days. Notify Finance Manager.")
        => new SemanticSearchResult(
            Rank: 1, Score: score, ChunkId: chunkId, DocumentId: docId,
            DocumentTitle: docTitle, FileName: "test.txt",
            Category: "Finance", SectionTitle: "Escalation",
            HeadingPath: "Finance > AR", StartPage: 1, EndPage: 1,
            Content: content, Source: "SOP", Version: "1.0", Language: "English");

    private (FakeRagLlmProvider llm, FakeRagSearchService search, KnowledgeRagService ragService)
        BuildRagService(
            IEnumerable<SemanticSearchResult> searchResults,
            string llmAnswer = "Answer. [1]",
            double minScore = 0.35)
    {
        var fakeLlm = new FakeRagLlmProvider(llmAnswer);
        var fakeSearch = new FakeRagSearchService(searchResults.ToList());
        var ragService = BuildRagServiceWithFakes(searchResults.ToArray(), fakeLlm, fakeSearch, minScore);
        return (fakeLlm, fakeSearch, ragService);
    }

    private KnowledgeRagService BuildRagServiceWithFakes(
        IEnumerable<SemanticSearchResult> searchResults,
        ILlmProvider? fakeLlm = null,
        IKnowledgeSearchService? fakeSearch = null,
        double minScore = 0.35)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            { "Rag:TopK", "5" },
            { "Rag:MinimumScore", minScore.ToString("F2") },
            { "Rag:MaximumSources", "5" },
            { "Rag:MaxContextCharacters", "14000" },
            { "Rag:MaxChunkCharacters", "3500" },
            { "Rag:MaxCitationSnippetCharacters", "300" },
            { "Rag:IncludeAdjacentChunks", "false" },
            { "Rag:MaxConversationTurns", "3" },
            { "Rag:Temperature", "0.1" }
        }).Build();

        return new KnowledgeRagService(
            searchService: fakeSearch ?? new FakeRagSearchService(searchResults.ToList()),
            llmProvider: fakeLlm ?? new FakeRagLlmProvider("Answer. [1]"),
            contextBuilder: new GroundingContextBuilder(),
            citationValidator: new CitationValidator(),
            conversationStore: new KnowledgeRagConversationStore(),
            configuration: config,
            logger: NullLogger<KnowledgeRagService>.Instance);
    }

    // =========================================================================
    // Fakes
    // =========================================================================

    private sealed class FakeRagSearchService : IKnowledgeSearchService
    {
        private readonly List<SemanticSearchResult> _results;
        public FakeRagSearchService(List<SemanticSearchResult> results) => _results = results;

        public Task<SemanticSearchResponse> SearchAsync(SemanticSearchRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new SemanticSearchResponse(request.Query, _results, 5L, _results.Count));
    }

    private sealed class FakeCountingSearchService : IKnowledgeSearchService
    {
        private readonly List<SemanticSearchResult> _results;
        public int CallCount { get; private set; }
        public FakeCountingSearchService(IEnumerable<SemanticSearchResult> results) => _results = results.ToList();

        public Task<SemanticSearchResponse> SearchAsync(SemanticSearchRequest request, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(new SemanticSearchResponse(request.Query, _results, 5L, _results.Count));
        }
    }

    private sealed class FakeFailingSearchService : IKnowledgeSearchService
    {
        private readonly bool _throwOnSearch;
        public FakeFailingSearchService(bool throwOnSearch) => _throwOnSearch = throwOnSearch;

        public Task<SemanticSearchResponse> SearchAsync(SemanticSearchRequest request, CancellationToken cancellationToken = default)
        {
            if (_throwOnSearch) throw new HttpRequestException("embedding service unavailable");
            return Task.FromResult(new SemanticSearchResponse(request.Query, Array.Empty<SemanticSearchResult>(), 0, 0));
        }
    }

    private sealed class FakeRagLlmProvider : ILlmProvider
    {
        private readonly string _firstAnswer;
        private readonly string? _retryAnswer;
        public int CallCount { get; private set; }

        public FakeRagLlmProvider(string firstAnswer, string? retryAnswer = null)
        {
            _firstAnswer = firstAnswer;
            _retryAnswer = retryAnswer;
        }

        public Task<LlmChatResponse> ChatAsync(LlmChatRequest request, CancellationToken cancellationToken = default)
        {
            CallCount++;
            var answer = (CallCount == 1 || _retryAnswer == null) ? _firstAnswer : _retryAnswer;
            return Task.FromResult(new LlmChatResponse { Content = answer });
        }
    }

    private sealed class FakeFailingLlmProvider : ILlmProvider
    {
        public Task<LlmChatResponse> ChatAsync(LlmChatRequest request, CancellationToken cancellationToken = default)
            => throw new HttpRequestException("Ollama unavailable");
    }
}
