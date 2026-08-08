using ERP.AI.Knowledge.Dtos;

namespace ERP.AI.Knowledge.Interfaces;

/// <summary>
/// Builds a grounding context from semantic search results.
/// Handles deduplication, adjacent-chunk expansion, context budgeting,
/// and SOURCE [N] block formatting for prompt injection resistance.
/// </summary>
public interface IGroundingContextBuilder
{
    /// <summary>
    /// Processes retrieved semantic search results into a formatted grounding
    /// context suitable for LLM consumption, with citation numbering and
    /// content deduplication applied.
    /// </summary>
    GroundingContext Build(
        IReadOnlyList<SemanticSearchResult> results,
        GroundingContextOptions options);
}
