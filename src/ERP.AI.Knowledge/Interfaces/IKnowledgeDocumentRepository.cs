using ERP.AI.Knowledge.Entities;
using ERP.AI.Knowledge.Enums;

namespace ERP.AI.Knowledge.Interfaces;

public interface IKnowledgeDocumentRepository
{
    Task<KnowledgeDocument> CreateAsync(KnowledgeDocument document, CancellationToken cancellationToken = default);
    Task<KnowledgeDocument?> GetByDocumentIdAsync(string documentId, CancellationToken cancellationToken = default);
    Task<KnowledgeDocument?> GetByHashAsync(string fileHash, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<KnowledgeDocument> Items, int TotalCount)> ListAsync(
        int page = 1,
        int pageSize = 20,
        DocumentStatus? status = null,
        string? category = null,
        string? search = null,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<KnowledgeDocument>> GetUnindexedAsync(CancellationToken cancellationToken = default);
    Task UpdateAsync(KnowledgeDocument document, CancellationToken cancellationToken = default);
    Task DeleteAsync(string documentId, CancellationToken cancellationToken = default);
}
