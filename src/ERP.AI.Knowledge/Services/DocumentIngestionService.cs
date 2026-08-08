using ERP.AI.Knowledge.Chunking;
using ERP.AI.Knowledge.Entities;
using ERP.AI.Knowledge.Enums;
using ERP.AI.Knowledge.Interfaces;
using ERP.AI.Knowledge.Parsers;
using ERP.AI.Knowledge.Storage;
using ERP.AI.Knowledge.Dtos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ERP.AI.Knowledge.Services;

public class DocumentIngestionService : IDocumentIngestionService
{
    private readonly IKnowledgeDocumentRepository _documentRepository;
    private readonly IKnowledgeChunkRepository _chunkRepository;
    private readonly IDocumentStorage _storage;
    private readonly IDocumentParserRegistry _parserRegistry;
    private readonly IDocumentChunker _chunker;
    private readonly IDocumentTextNormalizer _normalizer;
    private readonly ILogger<DocumentIngestionService> _logger;
    private readonly long _maxFileSizeByte;
    private readonly HashSet<string> _allowedExtensions;

    public DocumentIngestionService(
        IKnowledgeDocumentRepository documentRepository,
        IKnowledgeChunkRepository chunkRepository,
        IDocumentStorage storage,
        IDocumentParserRegistry parserRegistry,
        IDocumentChunker chunker,
        IDocumentTextNormalizer normalizer,
        IConfiguration configuration,
        ILogger<DocumentIngestionService> logger)
    {
        _documentRepository = documentRepository;
        _chunkRepository = chunkRepository;
        _storage = storage;
        _parserRegistry = parserRegistry;
        _chunker = chunker;
        _normalizer = normalizer;
        _logger = logger;

        var maxMb = long.TryParse(configuration["Knowledge:MaxFileSizeMb"], out var mb) ? mb : 25;
        _maxFileSizeByte = maxMb * 1024 * 1024;

        _allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".docx", ".txt", ".md", ".markdown"
        };
    }

    public async Task<KnowledgeDocument> IngestAsync(
        string originalFileName,
        string mimeType,
        Stream contentStream,
        DocumentUploadRequest request,
        string uploadedBy = "System",
        CancellationToken cancellationToken = default)
    {
        var ext = Path.GetExtension(originalFileName).ToLowerInvariant();
        if (!_allowedExtensions.Contains(ext))
        {
            throw new InvalidOperationException($"Unsupported file extension '{ext}'. Allowed extensions are: .pdf, .docx, .txt, .md.");
        }

        if (contentStream.Length > _maxFileSizeByte)
        {
            throw new InvalidOperationException($"File size exceeds the maximum allowed limit of {_maxFileSizeByte / (1024 * 1024)} MB.");
        }

        var fileHash = await _storage.CalculateHashAsync(contentStream, cancellationToken);
        var existing = await _documentRepository.GetByHashAsync(fileHash, cancellationToken);
        if (existing != null && existing.Status != DocumentStatus.Deleted)
        {
            throw new InvalidOperationException($"This document has already been uploaded (DocumentId: {existing.DocumentId}, Title: '{existing.Title}').");
        }

        var documentId = Guid.NewGuid().ToString("N");
        var savedFile = await _storage.SaveAsync(documentId, originalFileName, contentStream, cancellationToken);

        var title = !string.IsNullOrWhiteSpace(request.Title)
            ? request.Title
            : Path.GetFileNameWithoutExtension(originalFileName);

        var docEntity = new KnowledgeDocument
        {
            DocumentId = documentId,
            FileName = originalFileName,
            OriginalFileName = originalFileName,
            StoredFileName = savedFile.StoredFileName,
            FileExtension = ext,
            MimeType = mimeType,
            FileSize = savedFile.FileSize,
            FileHash = savedFile.FileHash,
            Title = title,
            Description = request.Description,
            Source = request.Source,
            Category = request.Category,
            Language = request.Language ?? "Auto",
            Version = request.Version,
            Status = DocumentStatus.Processing,
            ProcessingStage = ProcessingStage.Parsing,
            UploadedBy = uploadedBy,
            UploadedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _documentRepository.CreateAsync(docEntity, cancellationToken);

        try
        {
            await ProcessDocumentInternalAsync(docEntity, savedFile, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process document ingestion for DocumentId {DocumentId}", documentId);
            docEntity.Status = DocumentStatus.Failed;
            docEntity.ProcessingStage = ProcessingStage.Failed;
            docEntity.ProcessingError = ex.Message;
            docEntity.UpdatedAt = DateTime.UtcNow;
            await _documentRepository.UpdateAsync(docEntity, cancellationToken);
        }

        return docEntity;
    }

    public async Task<KnowledgeDocument> ReprocessAsync(string documentId, CancellationToken cancellationToken = default)
    {
        var docEntity = await _documentRepository.GetByDocumentIdAsync(documentId, cancellationToken);
        if (docEntity == null || docEntity.Status == DocumentStatus.Deleted)
        {
            throw new KeyNotFoundException($"Document with Id '{documentId}' was not found.");
        }

        _logger.LogInformation("Reprocessing document {DocumentId} ('{Title}')", documentId, docEntity.Title);

        docEntity.Status = DocumentStatus.Processing;
        docEntity.ProcessingStage = ProcessingStage.Parsing;
        docEntity.ProcessingError = null;
        docEntity.UpdatedAt = DateTime.UtcNow;
        await _documentRepository.UpdateAsync(docEntity, cancellationToken);

        try
        {
            // Remove previous chunks cleanly
            await _chunkRepository.DeleteByDocumentIdAsync(documentId, cancellationToken);

            using var fileStream = await _storage.OpenReadAsync(documentId, docEntity.StoredFileName, cancellationToken);
            var savedFile = new StoredDocumentFile(docEntity.StoredFileName, $"{documentId}/original/{docEntity.StoredFileName}", "", docEntity.FileSize, docEntity.FileHash);

            await ProcessDocumentInternalAsync(docEntity, savedFile, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reprocess document {DocumentId}", documentId);
            docEntity.Status = DocumentStatus.Failed;
            docEntity.ProcessingStage = ProcessingStage.Failed;
            docEntity.ProcessingError = ex.Message;
            docEntity.UpdatedAt = DateTime.UtcNow;
            await _documentRepository.UpdateAsync(docEntity, cancellationToken);
        }

        return docEntity;
    }

    private async Task ProcessDocumentInternalAsync(KnowledgeDocument docEntity, StoredDocumentFile savedFile, CancellationToken cancellationToken)
    {
        docEntity.ProcessingStage = ProcessingStage.Parsing;
        await _documentRepository.UpdateAsync(docEntity, cancellationToken);

        using var fileStream = await _storage.OpenReadAsync(docEntity.DocumentId, docEntity.StoredFileName, cancellationToken);
        var parseRequest = new DocumentParseRequest(docEntity.DocumentId, docEntity.FileExtension, docEntity.MimeType, docEntity.OriginalFileName, fileStream);

        var parser = _parserRegistry.GetParser(docEntity.FileExtension, docEntity.MimeType);
        var parsedDocument = await parser.ParseAsync(parseRequest, cancellationToken);

        docEntity.ProcessingStage = ProcessingStage.Normalization;
        await _documentRepository.UpdateAsync(docEntity, cancellationToken);

        docEntity.ProcessingStage = ProcessingStage.Chunking;
        await _documentRepository.UpdateAsync(docEntity, cancellationToken);

        var chunks = await _chunker.ChunkAsync(docEntity.DocumentId, parsedDocument, cancellationToken: cancellationToken);

        docEntity.ProcessingStage = ProcessingStage.Persistence;
        await _documentRepository.UpdateAsync(docEntity, cancellationToken);

        if (chunks.Count > 0)
        {
            await _chunkRepository.AddRangeAsync(chunks, cancellationToken);
        }

        var normalizedPlainText = _normalizer.Normalize(parsedDocument.PlainText);
        var wordCount = normalizedPlainText.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;

        docEntity.PageCount = parsedDocument.PageCount > 0 ? parsedDocument.PageCount : 1;
        docEntity.CharacterCount = normalizedPlainText.Length;
        docEntity.WordCount = wordCount;
        docEntity.ChunkCount = chunks.Count;
        docEntity.Status = DocumentStatus.Processed;
        docEntity.ProcessingStage = ProcessingStage.Completed;
        docEntity.ProcessedAt = DateTime.UtcNow;
        docEntity.UpdatedAt = DateTime.UtcNow;

        await _documentRepository.UpdateAsync(docEntity, cancellationToken);
        _logger.LogInformation("Document {DocumentId} successfully processed into {ChunkCount} chunks.", docEntity.DocumentId, chunks.Count);

        // Auto Index Embeddings into Qdrant (Phase 2.2)
        try
        {
            var indexingService = _parserRegistry as IKnowledgeIndexingService;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Automatic vector indexing skipped or failed for document {DocumentId}", docEntity.DocumentId);
        }
    }
}
