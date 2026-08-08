using ERP.AI.Knowledge.Dtos;
using ERP.AI.Knowledge.Entities;

namespace ERP.AI.Knowledge.Services;

public interface IDocumentIngestionService
{
    Task<KnowledgeDocument> IngestAsync(
        string originalFileName,
        string mimeType,
        Stream contentStream,
        DocumentUploadRequest request,
        string uploadedBy = "System",
        CancellationToken cancellationToken = default);

    Task<KnowledgeDocument> ReprocessAsync(string documentId, CancellationToken cancellationToken = default);
}
