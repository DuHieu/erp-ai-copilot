using ERP.AI.Knowledge.Dtos;

namespace ERP.AI.Knowledge.Interfaces;

/// <summary>
/// Orchestrates grounded RAG (Retrieval-Augmented Generation) answering from
/// the enterprise Knowledge Base. Retrieves relevant evidence from Qdrant,
/// assembles a grounding context, and generates a cited answer via local LLM.
/// </summary>
public interface IKnowledgeRagService
{
    /// <summary>
    /// Answers a natural-language question using only evidence retrieved from
    /// the Knowledge Base. Returns a deterministic refusal if no relevant
    /// evidence is found — the LLM is NOT called in that case.
    /// </summary>
    Task<RagChatResponse> AskAsync(RagChatRequest request, CancellationToken cancellationToken = default);
}
