using ERP.AI.Knowledge.Enums;

namespace ERP.AI.Knowledge.Entities;

public class KnowledgeDocument
{
    public int Id { get; set; }
    public string DocumentId { get; set; } = Guid.NewGuid().ToString("N");
    public string FileName { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string FileExtension { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string FileHash { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Source { get; set; }
    public string? Category { get; set; }
    public string? Language { get; set; } = "Auto";
    public string? Version { get; set; }

    public DocumentStatus Status { get; set; } = DocumentStatus.Uploaded;
    public ProcessingStage ProcessingStage { get; set; } = ProcessingStage.Validation;
    public string? ProcessingError { get; set; }

    public EmbeddingStatus EmbeddingStatus { get; set; } = EmbeddingStatus.NotIndexed;
    public string? EmbeddingModel { get; set; }
    public string? EmbeddingError { get; set; }
    public int EmbeddedChunkCount { get; set; }
    public DateTime? IndexedAt { get; set; }

    public int PageCount { get; set; }
    public int CharacterCount { get; set; }
    public int WordCount { get; set; }
    public int ChunkCount { get; set; }

    public string? UploadedBy { get; set; } = "System";
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<KnowledgeChunk> Chunks { get; set; } = new List<KnowledgeChunk>();
}
