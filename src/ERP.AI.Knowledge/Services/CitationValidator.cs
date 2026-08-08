using System.Text.RegularExpressions;
using ERP.AI.Knowledge.Dtos;
using ERP.AI.Knowledge.Interfaces;

namespace ERP.AI.Knowledge.Services;

/// <summary>
/// Validates that citation IDs referenced in an LLM-generated answer
/// correspond to sources that were actually supplied in the grounding context.
/// Prevents invented or hallucinated source references.
/// </summary>
public sealed class CitationValidator : ICitationValidator
{
    // Matches patterns like [1], [2], [12] — answer-local citation IDs
    private static readonly Regex CitationPattern = new(@"\[(\d+)\]", RegexOptions.Compiled);

    public CitationValidationResult Validate(string llmAnswer, IReadOnlyList<SourceCitationDto> sources)
    {
        if (string.IsNullOrWhiteSpace(llmAnswer) || sources == null || sources.Count == 0)
        {
            return new CitationValidationResult(
                HasValidCitations: false,
                HasUnknownIds: false,
                IsMissingCitations: !string.IsNullOrWhiteSpace(llmAnswer),
                UnknownIds: Array.Empty<int>());
        }

        var validIds = new HashSet<int>(sources.Select(s => s.CitationId));
        var referencedIds = ExtractCitationIds(llmAnswer);

        if (referencedIds.Count == 0)
        {
            return new CitationValidationResult(
                HasValidCitations: false,
                HasUnknownIds: false,
                IsMissingCitations: true,
                UnknownIds: Array.Empty<int>());
        }

        var unknownIds = referencedIds.Where(id => !validIds.Contains(id)).ToList();

        return new CitationValidationResult(
            HasValidCitations: referencedIds.Count > 0 && unknownIds.Count == 0,
            HasUnknownIds: unknownIds.Count > 0,
            IsMissingCitations: false,
            UnknownIds: unknownIds);
    }

    public bool HasAnyCitation(string answer)
    {
        if (string.IsNullOrWhiteSpace(answer))
            return false;

        return CitationPattern.IsMatch(answer);
    }

    public string SanitizeInvalidCitationIds(string answer, IReadOnlyList<SourceCitationDto> sources)
    {
        if (string.IsNullOrWhiteSpace(answer) || sources == null || sources.Count == 0)
            return answer ?? string.Empty;

        var validIds = new HashSet<int>(sources.Select(s => s.CitationId));

        return CitationPattern.Replace(answer, match =>
        {
            if (int.TryParse(match.Groups[1].Value, out var id) && validIds.Contains(id))
                return match.Value; // Keep valid citation

            return string.Empty; // Remove invalid citation
        });
    }

    private static IReadOnlyList<int> ExtractCitationIds(string text)
    {
        var ids = new HashSet<int>();
        foreach (Match match in CitationPattern.Matches(text))
        {
            if (int.TryParse(match.Groups[1].Value, out var id))
                ids.Add(id);
        }
        return ids.OrderBy(id => id).ToList();
    }
}
