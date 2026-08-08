namespace ERP.AI.Knowledge.Entities;

public class KnowledgeChunk
{
    public int Id { get; set; }
    public string ChunkId { get; set; } = Guid.NewGuid().ToString("N");
    public string DocumentId { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }

    public string Content { get; set; } = string.Empty;
    public string ContentHash { get; set; } = string.Empty;

    public int? PageNumber { get; set; }
    public int? StartPage { get; set; }
    public int? EndPage { get; set; }

    public string? SectionTitle { get; set; }
    public string? HeadingPath { get; set; }

    public int CharacterCount { get; set; }
    public int WordCount { get; set; }
    public int TokenEstimate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public KnowledgeDocument? Document { get; set; }
}
