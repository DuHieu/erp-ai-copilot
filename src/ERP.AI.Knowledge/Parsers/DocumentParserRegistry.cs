namespace ERP.AI.Knowledge.Parsers;

public interface IDocumentParserRegistry
{
    IDocumentParser GetParser(string fileExtension, string mimeType);
}

public class DocumentParserRegistry : IDocumentParserRegistry
{
    private readonly IEnumerable<IDocumentParser> _parsers;

    public DocumentParserRegistry(IEnumerable<IDocumentParser> parsers)
    {
        _parsers = parsers;
    }

    public IDocumentParser GetParser(string fileExtension, string mimeType)
    {
        var parser = _parsers.FirstOrDefault(p => p.CanParse(fileExtension, mimeType));
        if (parser == null)
        {
            throw new NotSupportedException($"No parser found for file extension '{fileExtension}' and MIME type '{mimeType}'. Supported formats are .pdf, .docx, .txt, .md.");
        }
        return parser;
    }
}
