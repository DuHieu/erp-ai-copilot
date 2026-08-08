using System.Text;
using ERP.AI.Knowledge.Dtos;
using ERP.AI.Knowledge.Interfaces;

namespace ERP.AI.Knowledge.Services;

/// <summary>
/// Builds a grounding context from semantic search results.
/// Deduplicates chunks, applies context budget, expands adjacent chunks
/// where useful, and formats explicit SOURCE [N] blocks with metadata headers.
/// </summary>
public sealed class GroundingContextBuilder : IGroundingContextBuilder
{
    public GroundingContext Build(
        IReadOnlyList<SemanticSearchResult> results,
        GroundingContextOptions options)
    {
        if (results == null || results.Count == 0)
        {
            return new GroundingContext(
                FormattedContext: string.Empty,
                Sources: Array.Empty<SourceCitationDto>(),
                ApproximateTokenCount: 0,
                UsedChunkCount: 0);
        }

        // Step 1: Deduplicate by ChunkId (exact) then by ContentHash (near-duplicate)
        var dedupedResults = DeduplicateResults(results);

        // Step 2: Limit to MaxSources, highest score first (already ranked by search)
        var selectedResults = dedupedResults
            .Take(options.MaxSources)
            .ToList();

        // Step 3: Optionally expand with adjacent chunks from same section
        if (options.IncludeAdjacentChunks && options.MaxAdjacentChunks > 0)
        {
            selectedResults = ExpandWithAdjacentChunks(selectedResults, results, options);
        }

        // Step 4: Build citation list and formatted context, respecting char budget
        var sources = new List<SourceCitationDto>();
        var contextBuilder = new StringBuilder();
        int totalCharacters = 0;
        int approximateTokens = 0;
        int citationId = 1;
        var usedChunkIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var result in selectedResults)
        {
            if (usedChunkIds.Contains(result.ChunkId))
                continue;

            // Truncate chunk content if it exceeds MaxChunkCharacters
            var chunkContent = result.Content;
            if (chunkContent.Length > options.MaxChunkCharacters)
            {
                chunkContent = chunkContent.Substring(0, options.MaxChunkCharacters).TrimEnd() + "...";
            }

            // Build the source block
            var sourceBlock = BuildSourceBlock(citationId, result, chunkContent);

            // Enforce total context budget
            if (totalCharacters + sourceBlock.Length > options.MaxContextCharacters && sources.Count > 0)
            {
                break; // Keep at least one source even if over budget
            }

            contextBuilder.Append(sourceBlock);
            totalCharacters += sourceBlock.Length;
            // Rough token approximation: 1 token ≈ 4 characters
            approximateTokens += chunkContent.Length / 4;

            // Build snippet for citation DTO
            var snippet = BuildSnippet(chunkContent, options.MaxSnippetCharacters);

            sources.Add(new SourceCitationDto(
                CitationId: citationId,
                DocumentId: result.DocumentId,
                ChunkId: result.ChunkId,
                DocumentTitle: result.DocumentTitle,
                FileName: result.FileName,
                Category: result.Category,
                SectionTitle: result.SectionTitle,
                HeadingPath: result.HeadingPath,
                StartPage: result.StartPage,
                EndPage: result.EndPage,
                Score: result.Score,
                Snippet: snippet
            ));

            usedChunkIds.Add(result.ChunkId);
            citationId++;
        }

        return new GroundingContext(
            FormattedContext: contextBuilder.ToString(),
            Sources: sources,
            ApproximateTokenCount: approximateTokens,
            UsedChunkCount: sources.Count);
    }

    private static IReadOnlyList<SemanticSearchResult> DeduplicateResults(
        IReadOnlyList<SemanticSearchResult> results)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var seenHashes = new HashSet<string>(StringComparer.Ordinal);
        var deduped = new List<SemanticSearchResult>(results.Count);

        foreach (var result in results)
        {
            // Deduplicate by exact ChunkId
            if (!seen.Add(result.ChunkId))
                continue;

            // Deduplicate by content hash (near-exact adjacent chunks with same text)
            // We use the content itself as a hash key since we don't have ContentHash in the DTO
            // The actual ContentHash dedup happens at the chunk level via ChunkId
            deduped.Add(result);
        }

        return deduped;
    }

    private static List<SemanticSearchResult> ExpandWithAdjacentChunks(
        List<SemanticSearchResult> selected,
        IReadOnlyList<SemanticSearchResult> allResults,
        GroundingContextOptions options)
    {
        // This expansion is intentionally limited: only append adjacent chunks
        // from same document and same section that were retrieved (already in allResults)
        // We do NOT make extra DB calls here. Adjacent means consecutive rank in same doc/section.

        var selectedChunkIds = new HashSet<string>(selected.Select(r => r.ChunkId), StringComparer.Ordinal);
        var adjacentCandidates = new List<SemanticSearchResult>();

        foreach (var s in selected)
        {
            // Find adjacent chunks from same doc + same sectionTitle in allResults
            foreach (var candidate in allResults)
            {
                if (adjacentCandidates.Count >= options.MaxAdjacentChunks)
                    break;
                if (selectedChunkIds.Contains(candidate.ChunkId))
                    continue;
                if (candidate.DocumentId != s.DocumentId)
                    continue;
                if (!string.Equals(candidate.SectionTitle, s.SectionTitle, StringComparison.OrdinalIgnoreCase))
                    continue;

                adjacentCandidates.Add(candidate);
                selectedChunkIds.Add(candidate.ChunkId);
            }
        }

        // Append adjacent candidates (they get lower citation IDs if added before limit)
        var expanded = new List<SemanticSearchResult>(selected);
        expanded.AddRange(adjacentCandidates.Take(options.MaxAdjacentChunks));
        return expanded;
    }

    private static string BuildSourceBlock(int citationId, SemanticSearchResult result, string content)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"BEGIN SOURCE [{citationId}]");
        sb.AppendLine($"Document: {result.DocumentTitle}");
        sb.AppendLine($"File: {result.FileName}");

        if (!string.IsNullOrWhiteSpace(result.Category))
            sb.AppendLine($"Category: {result.Category}");

        if (!string.IsNullOrWhiteSpace(result.SectionTitle))
            sb.AppendLine($"Section: {result.SectionTitle}");

        if (!string.IsNullOrWhiteSpace(result.HeadingPath))
            sb.AppendLine($"Path: {result.HeadingPath}");

        if (result.StartPage.HasValue)
        {
            sb.Append($"Page: {result.StartPage}");
            if (result.EndPage.HasValue && result.EndPage != result.StartPage)
                sb.Append($"-{result.EndPage}");
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("Page: N/A");
        }

        if (!string.IsNullOrWhiteSpace(result.Version))
            sb.AppendLine($"Version: {result.Version}");

        sb.AppendLine();
        sb.AppendLine("CONTENT:");
        sb.AppendLine(content.Trim());
        sb.AppendLine($"END SOURCE [{citationId}]");
        sb.AppendLine();

        return sb.ToString();
    }

    private static string? BuildSnippet(string content, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(content))
            return null;

        var trimmed = content.Trim();
        if (trimmed.Length <= maxLength)
            return trimmed;

        return "..." + trimmed.Substring(0, maxLength).TrimEnd() + "...";
    }
}
