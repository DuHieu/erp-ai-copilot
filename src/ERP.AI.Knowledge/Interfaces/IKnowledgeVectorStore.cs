using ERP.AI.Knowledge.Dtos;

namespace ERP.AI.Knowledge.Interfaces;

public interface IKnowledgeVectorStore
{
    Task EnsureCollectionAsync(int dimension, string distanceMetric = "Cosine", CancellationToken cancellationToken = default);
    Task UpsertAsync(IReadOnlyList<VectorPoint> points, CancellationToken cancellationToken = default);
    Task DeleteDocumentAsync(string documentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SemanticSearchResult>> SearchAsync(
        IReadOnlyList<float> queryVector,
        int topK,
        double minScore,
        string? category = null,
        string? language = null,
        string? documentId = null,
        CancellationToken cancellationToken = default);
}
