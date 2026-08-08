using ERP.AI.Knowledge.Dtos;

namespace ERP.AI.Knowledge.Interfaces;

public interface IEmbeddingService
{
    Task<EmbeddingResult> EmbedQueryAsync(string text, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EmbeddingResult>> EmbedDocumentsAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default);
}
