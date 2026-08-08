using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ERP.AI.Knowledge.Parsers;

public class DoclingServiceDocumentParser : IDocumentParser
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<DoclingServiceDocumentParser> _logger;
    private readonly string _endpoint;

    public DoclingServiceDocumentParser(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<DoclingServiceDocumentParser> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        var baseUrl = configuration["DocumentParser:Endpoint"] ?? "http://localhost:8000";
        _endpoint = $"{baseUrl.TrimEnd('/')}/parse";
    }

    public bool CanParse(string fileExtension, string mimeType)
    {
        var ext = fileExtension.ToLowerInvariant();
        return ext == ".pdf" || ext == ".docx" ||
               mimeType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase) ||
               mimeType.Equals("application/vnd.openxmlformats-officedocument.wordprocessingml.document", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ParsedDocument> ParseAsync(DocumentParseRequest request, CancellationToken cancellationToken = default)
    {
        request.ContentStream.Position = 0;

        using var content = new MultipartFormDataContent();
        using var streamContent = new StreamContent(request.ContentStream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(request.MimeType.Length > 0 ? request.MimeType : "application/octet-stream");
        content.Add(streamContent, "file", request.OriginalFileName);

        _logger.LogInformation("Sending parsing request to document-parser sidecar for file '{FileName}'", request.OriginalFileName);

        try
        {
            var response = await _httpClient.PostAsync(_endpoint, content, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var errText = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Document parser sidecar returned status code {StatusCode}: {ErrorText}", response.StatusCode, errText);
                throw new InvalidOperationException($"Document parser sidecar error ({response.StatusCode}): {errText}");
            }

            var jsonStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var result = await JsonSerializer.DeserializeAsync<DoclingParseResponse>(jsonStream, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }, cancellationToken);

            if (result == null)
            {
                throw new InvalidOperationException("Empty response from document parser sidecar.");
            }

            var title = !string.IsNullOrWhiteSpace(result.Title)
                ? result.Title
                : Path.GetFileNameWithoutExtension(request.OriginalFileName);

            var sections = (result.Sections ?? new List<DoclingSectionDto>())
                .Select(s => new ParsedDocumentSection(
                    Title: string.IsNullOrWhiteSpace(s.Title) ? title : s.Title,
                    Content: s.Text ?? string.Empty,
                    PageNumber: s.PageNumber,
                    HeadingPath: s.HeadingPath
                ))
                .ToList();

            var pages = (result.Pages ?? new List<DoclingPageDto>())
                .Select(p => new ParsedDocumentPage(p.PageNumber, p.Text ?? string.Empty))
                .ToList();

            return new ParsedDocument(title, result.Text ?? string.Empty, result.PageCount, sections, pages);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogError(ex, "Failed to connect to document parser sidecar at {Endpoint}", _endpoint);
            throw new InvalidOperationException($"Document parser service unreachable at {_endpoint}. Ensure document-parser sidecar is running.", ex);
        }
    }

    private class DoclingParseResponse
    {
        public string? Title { get; set; }
        public string? Text { get; set; }
        public int PageCount { get; set; }
        public List<DoclingSectionDto>? Sections { get; set; }
        public List<DoclingPageDto>? Pages { get; set; }
    }

    private class DoclingSectionDto
    {
        public string? Title { get; set; }
        public string? Text { get; set; }
        public int? PageNumber { get; set; }
        public string? HeadingPath { get; set; }
    }

    private class DoclingPageDto
    {
        public int PageNumber { get; set; }
        public string? Text { get; set; }
    }
}
