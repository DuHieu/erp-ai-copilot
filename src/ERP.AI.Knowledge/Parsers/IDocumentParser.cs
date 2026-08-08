namespace ERP.AI.Knowledge.Parsers;

public record DocumentParseRequest(
    string DocumentId,
    string FileExtension,
    string MimeType,
    string OriginalFileName,
    Stream ContentStream
);

public record ParsedDocumentSection(
    string Title,
    string Content,
    int? PageNumber = null,
    string? HeadingPath = null
);

public record ParsedDocumentPage(
    int PageNumber,
    string Content
);

public record ParsedDocument(
    string Title,
    string PlainText,
    int PageCount,
    IReadOnlyList<ParsedDocumentSection> Sections,
    IReadOnlyList<ParsedDocumentPage> Pages
);

public interface IDocumentParser
{
    bool CanParse(string fileExtension, string mimeType);
    Task<ParsedDocument> ParseAsync(DocumentParseRequest request, CancellationToken cancellationToken = default);
}
