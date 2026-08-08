using ERP.AI.Knowledge.Dtos;

namespace ERP.AI.Knowledge.Interfaces;

/// <summary>
/// Validates that an LLM-generated answer references only the citation IDs
/// that were actually supplied in the grounding context.
/// Prevents the LLM from inventing or hallucinating source references.
/// </summary>
public interface ICitationValidator
{
    /// <summary>
    /// Inspects the LLM answer for citation references ([1], [2], ...) and
    /// validates them against the provided source citations.
    /// </summary>
    CitationValidationResult Validate(string llmAnswer, IReadOnlyList<SourceCitationDto> sources);

    /// <summary>
    /// Returns true if the answer contains at least one [N] citation pattern.
    /// </summary>
    bool HasAnyCitation(string answer);

    /// <summary>
    /// Returns the answer with all invalid (out-of-range) citation ID references
    /// removed. Does not throw; degrades gracefully.
    /// </summary>
    string SanitizeInvalidCitationIds(string answer, IReadOnlyList<SourceCitationDto> sources);
}
