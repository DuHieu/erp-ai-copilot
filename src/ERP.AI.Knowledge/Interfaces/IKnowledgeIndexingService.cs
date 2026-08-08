using ERP.AI.Knowledge.Entities;

namespace ERP.AI.Knowledge.Interfaces;

public interface IKnowledgeIndexingService
{
    Task<KnowledgeDocument> IndexDocumentAsync(string documentId, CancellationToken cancellationToken = default);
    Task<int> IndexUnindexedDocumentsAsync(CancellationToken cancellationToken = default);
    Task RebuildIndexAsync(CancellationToken cancellationToken = default);
}
