using ERP.AI.Knowledge.Enums;

namespace ERP.AI.Knowledge.Dtos;

public record DocumentUploadRequest(
    string Title,
    string? Description = null,
    string? Category = null,
    string? Source = null,
    string? Language = "Auto",
    string? Version = null
);

public record DocumentUploadResponse(
    string DocumentId,
    string FileName,
    DocumentStatus Status,
    ProcessingStage ProcessingStage,
    DateTime UploadedAt
);

public record DocumentDetailResponse(
    string DocumentId,
    string Title,
    string FileName,
    string OriginalFileName,
    string FileExtension,
    string MimeType,
    long FileSize,
    string FileHash,
    DocumentStatus Status,
    ProcessingStage ProcessingStage,
    string? ProcessingError,
    EmbeddingStatus EmbeddingStatus,
    string? EmbeddingModel,
    string? EmbeddingError,
    int EmbeddedChunkCount,
    DateTime? IndexedAt,
    int PageCount,
    int CharacterCount,
    int WordCount,
    int ChunkCount,
    string? Category,
    string? Source,
    string? Language,
    string? Version,
    string? Description,
    string? UploadedBy,
    DateTime UploadedAt,
    DateTime? ProcessedAt,
    DateTime UpdatedAt
);

public record DocumentSummaryDto(
    string DocumentId,
    string Title,
    string FileName,
    string FileExtension,
    long FileSize,
    DocumentStatus Status,
    ProcessingStage ProcessingStage,
    EmbeddingStatus EmbeddingStatus,
    int PageCount,
    int ChunkCount,
    string? Category,
    DateTime UploadedAt
);

public record DocumentChunkDto(
    string ChunkId,
    string DocumentId,
    int ChunkIndex,
    string Content,
    string ContentHash,
    int? PageNumber,
    int? StartPage,
    int? EndPage,
    string? SectionTitle,
    string? HeadingPath,
    int CharacterCount,
    int WordCount,
    int TokenEstimate,
    DateTime CreatedAt
);

public record PaginatedList<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize
)
{
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
}

// --- Phase 2.2 Semantic Search DTOs ---

public record SemanticSearchRequest(
    string Query,
    int TopK = 5,
    double MinimumScore = 0.35,
    string? Category = null,
    string? Language = null,
    string? DocumentId = null
);

public record SemanticSearchResult(
    int Rank,
    double Score,
    string ChunkId,
    string DocumentId,
    string DocumentTitle,
    string FileName,
    string? Category,
    string? SectionTitle,
    string? HeadingPath,
    int? StartPage,
    int? EndPage,
    string Content,
    string? Source,
    string? Version,
    string? Language
);

public record SemanticSearchResponse(
    string Query,
    IReadOnlyList<SemanticSearchResult> Results,
    long DurationMs,
    int TotalCandidates
);

public record EmbeddingResult(
    IReadOnlyList<float> Vector,
    int Dimension,
    string Model
);

public record VectorPoint(
    string PointId,
    IReadOnlyList<float> Vector,
    Dictionary<string, object> Payload
);

// --- Phase 2.3 Grounded RAG DTOs ---

public record RagChatRequest(
    string Question,
    string? ConversationId = null,
    string? Category = null,
    string? Language = null,
    IReadOnlyList<string>? DocumentIds = null
);

public record SourceCitationDto(
    int CitationId,
    string DocumentId,
    string ChunkId,
    string DocumentTitle,
    string FileName,
    string? Category,
    string? SectionTitle,
    string? HeadingPath,
    int? StartPage,
    int? EndPage,
    double Score,
    string? Snippet
);

public record RagChatResponse(
    string Answer,
    bool Grounded,
    bool NoEvidence,
    IReadOnlyList<SourceCitationDto> Sources,
    int RetrievedChunkCount,
    int UsedChunkCount,
    string ConversationId,
    long DurationMs,
    string TraceId,
    long RetrievalDurationMs = 0,
    long GenerationDurationMs = 0
);

public record GroundingContext(
    string FormattedContext,
    IReadOnlyList<SourceCitationDto> Sources,
    int ApproximateTokenCount,
    int UsedChunkCount
);

public record GroundingContextOptions(
    int MaxSources = 5,
    int MaxContextCharacters = 14000,
    int MaxChunkCharacters = 3500,
    int MaxSnippetCharacters = 300,
    bool IncludeAdjacentChunks = true,
    int MaxAdjacentChunks = 1
);

public class RagOptions
{
    public int TopK { get; set; } = 6;
    public double MinimumScore { get; set; } = 0.35;
    public int MaximumSources { get; set; } = 5;
    public int MaxContextCharacters { get; set; } = 14000;
    public int MaxChunkCharacters { get; set; } = 3500;
    public int MaxCitationSnippetCharacters { get; set; } = 300;
    public bool IncludeAdjacentChunks { get; set; } = true;
    public int MaxAdjacentChunks { get; set; } = 1;
    public int MaxConversationTurns { get; set; } = 6;
    public double Temperature { get; set; } = 0.1;
    public int MaxOutputTokens { get; set; } = 800;
}

public record CitationValidationResult(
    bool HasValidCitations,
    bool HasUnknownIds,
    bool IsMissingCitations,
    IReadOnlyList<int> UnknownIds
);
