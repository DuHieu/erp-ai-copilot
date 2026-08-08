using System.Text;
using System.Text.RegularExpressions;

namespace ERP.AI.Knowledge.Parsers;

public class MarkdownDocumentParser : IDocumentParser
{
    private static readonly Regex HeadingRegex = new Regex(@"^(#{1,6})\s+(.+)$", RegexOptions.Compiled);

    public bool CanParse(string fileExtension, string mimeType)
    {
        var ext = fileExtension.ToLowerInvariant();
        return ext == ".md" || ext == ".markdown" || mimeType.Equals("text/markdown", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ParsedDocument> ParseAsync(DocumentParseRequest request, CancellationToken cancellationToken = default)
    {
        request.ContentStream.Position = 0;
        using var reader = new StreamReader(request.ContentStream, Encoding.UTF8, leaveOpen: true);
        var fullText = await reader.ReadToEndAsync(cancellationToken);

        var docTitle = Path.GetFileNameWithoutExtension(request.OriginalFileName);
        var lines = fullText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

        var sections = new List<ParsedDocumentSection>();
        var headingStack = new List<(int Level, string Title)>();

        var currentSectionTitle = docTitle;
        var currentContent = new StringBuilder();

        foreach (var line in lines)
        {
            var match = HeadingRegex.Match(line.TrimEnd());
            if (match.Success)
            {
                var textSoFar = currentContent.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(textSoFar))
                {
                    var headingPath = headingStack.Count > 0
                        ? string.Join(" > ", headingStack.Select(h => h.Title))
                        : docTitle;

                    sections.Add(new ParsedDocumentSection(
                        Title: currentSectionTitle,
                        Content: textSoFar,
                        PageNumber: 1,
                        HeadingPath: headingPath
                    ));
                    currentContent.Clear();
                }

                var level = match.Groups[1].Value.Length;
                var headingText = match.Groups[2].Value.Trim();

                while (headingStack.Count > 0 && headingStack[^1].Level >= level)
                {
                    headingStack.RemoveAt(headingStack.Count - 1);
                }
                headingStack.Add((level, headingText));

                currentSectionTitle = headingText;
                if (level == 1)
                {
                    docTitle = headingText;
                }
                continue;
            }

            currentContent.AppendLine(line);
        }

        var remainingText = currentContent.ToString().Trim();
        if (!string.IsNullOrWhiteSpace(remainingText))
        {
            var headingPath = headingStack.Count > 0
                ? string.Join(" > ", headingStack.Select(h => h.Title))
                : docTitle;

            sections.Add(new ParsedDocumentSection(
                Title: currentSectionTitle,
                Content: remainingText,
                PageNumber: 1,
                HeadingPath: headingPath
            ));
        }

        var pages = new List<ParsedDocumentPage>
        {
            new ParsedDocumentPage(1, fullText)
        };

        return new ParsedDocument(docTitle, fullText, 1, sections, pages);
    }
}
