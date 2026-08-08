using ERP.AI.Knowledge.Entities;
using ERP.AI.Knowledge.Parsers;

namespace ERP.AI.Knowledge.Chunking;

public record ChunkingOptions(
    int MaxCharacters = 2500,
    int OverlapCharacters = 300,
    int MinimumCharacters = 200
);

public interface IDocumentChunker
{
    Task<IReadOnlyList<KnowledgeChunk>> ChunkAsync(
        string documentId,
        ParsedDocument document,
        ChunkingOptions? options = null,
        CancellationToken cancellationToken = default);
}
