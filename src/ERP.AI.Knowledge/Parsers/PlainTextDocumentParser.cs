using System.Text;

namespace ERP.AI.Knowledge.Parsers;

public class PlainTextDocumentParser : IDocumentParser
{
    public bool CanParse(string fileExtension, string mimeType)
    {
        var ext = fileExtension.ToLowerInvariant();
        return ext == ".txt" || mimeType.Equals("text/plain", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ParsedDocument> ParseAsync(DocumentParseRequest request, CancellationToken cancellationToken = default)
    {
        request.ContentStream.Position = 0;
        using var reader = new StreamReader(request.ContentStream, Encoding.UTF8, leaveOpen: true);
        var text = await reader.ReadToEndAsync(cancellationToken);

        var title = Path.GetFileNameWithoutExtension(request.OriginalFileName);
        var paragraphs = text.Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries);

        var sections = new List<ParsedDocumentSection>();
        for (int i = 0; i < paragraphs.Length; i++)
        {
            var p = paragraphs[i].Trim();
            if (!string.IsNullOrWhiteSpace(p))
            {
                sections.Add(new ParsedDocumentSection(
                    Title: $"Paragraph {i + 1}",
                    Content: p,
                    PageNumber: 1,
                    HeadingPath: title
                ));
            }
        }

        var pages = new List<ParsedDocumentPage>
        {
            new ParsedDocumentPage(1, text)
        };

        return new ParsedDocument(title, text, 1, sections, pages);
    }
}
