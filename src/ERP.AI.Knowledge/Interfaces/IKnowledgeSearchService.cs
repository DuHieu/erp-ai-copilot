using ERP.AI.Knowledge.Dtos;

namespace ERP.AI.Knowledge.Interfaces;

public interface IKnowledgeSearchService
{
    Task<SemanticSearchResponse> SearchAsync(SemanticSearchRequest request, CancellationToken cancellationToken = default);
}
