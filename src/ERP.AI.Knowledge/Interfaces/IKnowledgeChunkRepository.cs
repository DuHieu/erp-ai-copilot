using ERP.AI.Knowledge.Entities;

namespace ERP.AI.Knowledge.Interfaces;

public interface IKnowledgeChunkRepository
{
    Task AddRangeAsync(IEnumerable<KnowledgeChunk> chunks, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<KnowledgeChunk> Items, int TotalCount)> GetByDocumentIdAsync(
        string documentId,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);
    Task DeleteByDocumentIdAsync(string documentId, CancellationToken cancellationToken = default);
}
