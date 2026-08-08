using System.Text;
using ERP.AI.Knowledge.Chunking;
using ERP.AI.Knowledge.Dtos;
using ERP.AI.Knowledge.Entities;
using ERP.AI.Knowledge.Enums;
using ERP.AI.Knowledge.Interfaces;
using ERP.AI.Knowledge.Parsers;
using ERP.AI.Knowledge.Services;
using ERP.AI.Knowledge.Storage;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ERP.AI.Knowledge.Tests;

public class KnowledgeIngestionTests
{
    [Fact]
    public void Normalizer_Should_Clean_Control_Characters_And_Normalize_Newlines()
    {
        var normalizer = new DocumentTextNormalizer();
        var rawText = "Line 1\r\nLine 2\x00\x07\n\n\n\nLine 3   ";

        var result = normalizer.Normalize(rawText);

        result.Should().Be("Line 1\nLine 2\n\nLine 3");
    }

    [Fact]
    public async Task PlainTextParser_Should_Extract_Title_And_Paragraph_Sections()
    {
        var parser = new PlainTextDocumentParser();
        var text = "Paragraph One Content.\n\nParagraph Two Content.";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));
        var req = new DocumentParseRequest("doc123", ".txt", "text/plain", "procedure.txt", stream);

        var result = await parser.ParseAsync(req);

        result.Title.Should().Be("procedure");
        result.PageCount.Should().Be(1);
        result.Sections.Should().HaveCount(2);
        result.Sections[0].Title.Should().Be("Paragraph 1");
        result.Sections[0].Content.Should().Be("Paragraph One Content.");
        result.Sections[1].Content.Should().Be("Paragraph Two Content.");
    }

    [Fact]
    public async Task MarkdownParser_Should_Preserve_Heading_Hierarchy_Path()
    {
        var parser = new MarkdownDocumentParser();
        var mdText = "# Policy Title\n## Section 1: Intro\nIntro text\n### Subsection A\nSub text";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(mdText));
        var req = new DocumentParseRequest("doc456", ".md", "text/markdown", "policy.md", stream);

        var result = await parser.ParseAsync(req);

        result.Sections.Should().HaveCount(2);
        result.Sections[0].Title.Should().Be("Section 1: Intro");
        result.Sections[0].HeadingPath.Should().Be("Policy Title > Section 1: Intro");
        result.Sections[1].Title.Should().Be("Subsection A");
        result.Sections[1].HeadingPath.Should().Be("Policy Title > Section 1: Intro > Subsection A");
    }

    [Fact]
    public async Task Chunker_Should_Generate_Chunks_With_Hashes_And_Index()
    {
        var normalizer = new DocumentTextNormalizer();
        var chunker = new StructureAwareChunker(normalizer);

        var parsedDoc = new ParsedDocument("Test Title", "Sample text", 1, new List<ParsedDocumentSection>
        {
            new ParsedDocumentSection("Section A", "Content for section A", 1, "Test Title > Section A")
        }, new List<ParsedDocumentPage>());

        var chunks = await chunker.ChunkAsync("doc789", parsedDoc, new ChunkingOptions(2500, 300));

        chunks.Should().HaveCount(1);
        chunks[0].DocumentId.Should().Be("doc789");
        chunks[0].ChunkIndex.Should().Be(0);
        chunks[0].Content.Should().Be("Content for section A");
        chunks[0].ContentHash.Should().NotBeNullOrEmpty();
        chunks[0].WordCount.Should().Be(4);
    }

    [Fact]
    public async Task LocalStorage_Should_Save_Read_ComputeHash_And_Delete()
    {
        var tempFolder = Path.Combine(Path.GetTempPath(), "erp_test_knowledge_" + Guid.NewGuid().ToString("N"));
        var inMemoryConfig = new Dictionary<string, string?>
        {
            {"Knowledge:StoragePath", tempFolder}
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemoryConfig).Build();
        var storage = new LocalDocumentStorage(config);

        var fileContent = "Unit Test Content";
        using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(fileContent)))
        {
            var savedFile = await storage.SaveAsync("doc101", "test.txt", stream);
            savedFile.StoredFileName.Should().Be("doc101.txt");
            savedFile.FileSize.Should().Be(fileContent.Length);
        }

        var exists = await storage.ExistsAsync("doc101", "doc101.txt");
        exists.Should().BeTrue();

        using (var readStream = await storage.OpenReadAsync("doc101", "doc101.txt"))
        using (var reader = new StreamReader(readStream))
        {
            var readContent = await reader.ReadToEndAsync();
            readContent.Should().Be(fileContent);
        } // Explicitly closed readStream

        await storage.DeleteAsync("doc101");
        var existsAfterDelete = await storage.ExistsAsync("doc101", "doc101.txt");
        existsAfterDelete.Should().BeFalse();
    }

    [Fact]
    public async Task IngestionService_Should_Reject_Unsupported_Extensions()
    {
        var docRepo = new Mock<IKnowledgeDocumentRepository>();
        var chunkRepo = new Mock<IKnowledgeChunkRepository>();
        var storage = new Mock<IDocumentStorage>();
        var registry = new Mock<IDocumentParserRegistry>();
        var chunker = new Mock<IDocumentChunker>();
        var normalizer = new Mock<IDocumentTextNormalizer>();
        var config = new ConfigurationBuilder().Build();
        var logger = NullLogger<DocumentIngestionService>.Instance;

        var service = new DocumentIngestionService(docRepo.Object, chunkRepo.Object, storage.Object, registry.Object, chunker.Object, normalizer.Object, config, logger);

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("fake exe content"));
        var req = new DocumentUploadRequest("Malware", null, null, null, null, null);

        var act = async () => await service.IngestAsync("file.exe", "application/octet-stream", stream, req);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Unsupported file extension*");
    }

    [Fact]
    public async Task IngestionService_Should_Reject_Duplicate_SHA256_Hash()
    {
        var docRepo = new Mock<IKnowledgeDocumentRepository>();
        var chunkRepo = new Mock<IKnowledgeChunkRepository>();
        var storage = new Mock<IDocumentStorage>();
        var registry = new Mock<IDocumentParserRegistry>();
        var chunker = new Mock<IDocumentChunker>();
        var normalizer = new Mock<IDocumentTextNormalizer>();
        var config = new ConfigurationBuilder().Build();
        var logger = NullLogger<DocumentIngestionService>.Instance;

        storage.Setup(s => s.CalculateHashAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("duplicatehash123");

        docRepo.Setup(d => d.GetByHashAsync("duplicatehash123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KnowledgeDocument { DocumentId = "existing123", Title = "Existing Doc", Status = DocumentStatus.Processed });

        var service = new DocumentIngestionService(docRepo.Object, chunkRepo.Object, storage.Object, registry.Object, chunker.Object, normalizer.Object, config, logger);

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("content"));
        var req = new DocumentUploadRequest("Duplicate Doc", null, null, null, null, null);

        var act = async () => await service.IngestAsync("procedure.txt", "text/plain", stream, req);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*already been uploaded*");
    }
}
