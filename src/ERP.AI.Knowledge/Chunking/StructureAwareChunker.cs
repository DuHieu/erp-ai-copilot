using System.Security.Cryptography;
using System.Text;
using ERP.AI.Knowledge.Entities;
using ERP.AI.Knowledge.Parsers;
using ERP.AI.Knowledge.Services;

namespace ERP.AI.Knowledge.Chunking;

public class StructureAwareChunker : IDocumentChunker
{
    private readonly IDocumentTextNormalizer _normalizer;

    public StructureAwareChunker(IDocumentTextNormalizer normalizer)
    {
        _normalizer = normalizer;
    }

    public Task<IReadOnlyList<KnowledgeChunk>> ChunkAsync(
        string documentId,
        ParsedDocument document,
        ChunkingOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new ChunkingOptions();
        var chunks = new List<KnowledgeChunk>();
        int chunkIndex = 0;

        if (document.Sections.Count > 0)
        {
            foreach (var section in document.Sections)
            {
                var normalizedContent = _normalizer.Normalize(section.Content);
                if (string.IsNullOrWhiteSpace(normalizedContent))
                {
                    continue;
                }

                if (normalizedContent.Length <= options.MaxCharacters)
                {
                    chunks.Add(CreateChunk(documentId, chunkIndex++, normalizedContent, section.Title, section.HeadingPath, section.PageNumber, section.PageNumber));
                }
                else
                {
                    // Split long section into overlapping window chunks
                    var subChunks = SplitTextIntoChunks(normalizedContent, options.MaxCharacters, options.OverlapCharacters);
                    foreach (var sub in subChunks)
                    {
                        chunks.Add(CreateChunk(documentId, chunkIndex++, sub, section.Title, section.HeadingPath, section.PageNumber, section.PageNumber));
                    }
                }
            }
        }
        else
        {
            // Fallback: full document normalized text chunking
            var normalizedText = _normalizer.Normalize(document.PlainText);
            if (!string.IsNullOrWhiteSpace(normalizedText))
            {
                var textChunks = SplitTextIntoChunks(normalizedText, options.MaxCharacters, options.OverlapCharacters);
                foreach (var sub in textChunks)
                {
                    chunks.Add(CreateChunk(documentId, chunkIndex++, sub, document.Title, document.Title, 1, document.PageCount));
                }
            }
        }

        return Task.FromResult<IReadOnlyList<KnowledgeChunk>>(chunks);
    }

    private static List<string> SplitTextIntoChunks(string text, int maxChars, int overlapChars)
    {
        var result = new List<string>();
        int start = 0;

        while (start < text.Length)
        {
            int length = Math.Min(maxChars, text.Length - start);

            // Attempt to break at paragraph or newline boundary if possible
            if (start + length < text.Length)
            {
                int lastSpace = text.LastIndexOf("\n\n", start + length, length, StringComparison.Ordinal);
                if (lastSpace == -1 || lastSpace <= start + (maxChars / 2))
                {
                    lastSpace = text.LastIndexOf('\n', start + length - 1, length / 2);
                }
                if (lastSpace == -1 || lastSpace <= start + (maxChars / 2))
                {
                    lastSpace = text.LastIndexOf(' ', start + length - 1, length / 2);
                }

                if (lastSpace > start)
                {
                    length = lastSpace - start;
                }
            }

            var chunkText = text.Substring(start, length).Trim();
            if (!string.IsNullOrWhiteSpace(chunkText))
            {
                result.Add(chunkText);
            }

            start += Math.Max(1, length - overlapChars);
        }

        return result;
    }

    private static KnowledgeChunk CreateChunk(
        string documentId,
        int chunkIndex,
        string content,
        string? sectionTitle,
        string? headingPath,
        int? startPage,
        int? endPage)
    {
        var wordCount = content.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
        var charCount = content.Length;

        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(content));
        var contentHash = Convert.ToHexString(hashBytes).ToLowerInvariant();

        return new KnowledgeChunk
        {
            ChunkId = Guid.NewGuid().ToString("N"),
            DocumentId = documentId,
            ChunkIndex = chunkIndex,
            Content = content,
            ContentHash = contentHash,
            PageNumber = startPage,
            StartPage = startPage,
            EndPage = endPage,
            SectionTitle = sectionTitle,
            HeadingPath = headingPath,
            CharacterCount = charCount,
            WordCount = wordCount,
            TokenEstimate = charCount / 4,
            CreatedAt = DateTime.UtcNow
        };
    }
}
