using ERP.AI.Core.Interfaces;
using ERP.AI.Knowledge.Dtos;
using ERP.AI.Knowledge.Enums;
using ERP.AI.Knowledge.Interfaces;
using ERP.AI.Knowledge.Services;
using ERP.AI.Knowledge.Storage;
using Microsoft.AspNetCore.Mvc;

namespace ERP.AI.Api.Controllers;

[ApiController]
[Route("api/knowledge")]
public class KnowledgeController : ControllerBase
{
    private readonly IDocumentIngestionService _ingestionService;
    private readonly IKnowledgeDocumentRepository _documentRepository;
    private readonly IKnowledgeChunkRepository _chunkRepository;
    private readonly IDocumentStorage _storage;
    private readonly IKnowledgeIndexingService _indexingService;
    private readonly IKnowledgeSearchService _searchService;
    private readonly IKnowledgeVectorStore _vectorStore;
    private readonly IKnowledgeRagService _ragService;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<KnowledgeController> _logger;

    public KnowledgeController(
        IDocumentIngestionService ingestionService,
        IKnowledgeDocumentRepository documentRepository,
        IKnowledgeChunkRepository chunkRepository,
        IDocumentStorage storage,
        IKnowledgeIndexingService indexingService,
        IKnowledgeSearchService searchService,
        IKnowledgeVectorStore vectorStore,
        IKnowledgeRagService ragService,
        IServiceScopeFactory serviceScopeFactory,
        ICurrentUser currentUser,
        ILogger<KnowledgeController> logger)
    {
        _ingestionService = ingestionService;
        _documentRepository = documentRepository;
        _chunkRepository = chunkRepository;
        _storage = storage;
        _indexingService = indexingService;
        _searchService = searchService;
        _vectorStore = vectorStore;
        _ragService = ragService;
        _serviceScopeFactory = serviceScopeFactory;
        _currentUser = currentUser;
        _logger = logger;
    }

    /// <summary>
    /// Ask the Knowledge Base a natural-language question.
    /// Retrieves relevant evidence from indexed documents and generates a
    /// grounded, source-cited answer using the local LLM.
    /// Returns a deterministic refusal (noEvidence=true) if no relevant
    /// evidence is found — the LLM is NOT called in that case.
    /// </summary>
    [HttpPost("ask")]
    [ProducesResponseType(typeof(RagChatResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Ask([FromBody] RagChatRequest request, CancellationToken cancellationToken = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Question))
        {
            return BadRequest(new { error = "Question is required." });
        }

        if (request.Question.Trim().Length > 4000)
        {
            return BadRequest(new { error = "Question exceeds maximum length of 4000 characters." });
        }

        try
        {
            var response = await _ragService.AskAsync(request, cancellationToken);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("unavailable"))
        {
            return StatusCode(503, new { error = "Knowledge RAG service is currently unavailable.", details = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(503, new { error = "An unexpected error occurred during knowledge retrieval.", details = ex.Message });
        }
    }

    /// <summary>
    /// Execute semantic vector similarity search against enterprise knowledge base.
    /// </summary>
    [HttpPost("search")]
    [ProducesResponseType(typeof(SemanticSearchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Search([FromBody] SemanticSearchRequest request, CancellationToken cancellationToken = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Query))
        {
            return BadRequest(new { error = "Search query is required." });
        }

        try
        {
            var response = await _searchService.SearchAsync(request, cancellationToken);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(503, new { error = "Semantic search service unavailable.", details = ex.Message });
        }
    }

    /// <summary>
    /// Upload and ingest a new enterprise document (.pdf, .docx, .txt, .md).
    /// </summary>
    [HttpPost("documents")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(DocumentUploadResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UploadDocument(
        IFormFile file,
        [FromForm] string? title,
        [FromForm] string? description,
        [FromForm] string? category,
        [FromForm] string? source,
        [FromForm] string? language = "Auto",
        [FromForm] string? version = null,
        CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { error = "No file was uploaded." });
        }

        var uploadRequest = new DocumentUploadRequest(
            Title: title ?? Path.GetFileNameWithoutExtension(file.FileName),
            Description: description,
            Category: category,
            Source: source,
            Language: language,
            Version: version
        );

        try
        {
            using var stream = file.OpenReadStream();
            var doc = await _ingestionService.IngestAsync(
                originalFileName: file.FileName,
                mimeType: file.ContentType,
                contentStream: stream,
                request: uploadRequest,
                uploadedBy: _currentUser.UserId,
                cancellationToken: cancellationToken
            );

            QueueDocumentIndexing(doc.DocumentId);

            var response = new DocumentUploadResponse(
                doc.DocumentId,
                doc.FileName,
                doc.Status,
                doc.ProcessingStage,
                doc.UploadedAt
            );

            return CreatedAtAction(nameof(GetDocumentById), new { documentId = doc.DocumentId }, response);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already been uploaded"))
        {
            return Conflict(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// List uploaded documents with pagination, category filter, and search.
    /// </summary>
    [HttpGet("documents")]
    [ProducesResponseType(typeof(PaginatedList<DocumentSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListDocuments(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] DocumentStatus? status = null,
        [FromQuery] string? category = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var (items, totalCount) = await _documentRepository.ListAsync(page, pageSize, status, category, search, cancellationToken);
        var dtos = items.Select(d => new DocumentSummaryDto(
            d.DocumentId,
            d.Title,
            d.FileName,
            d.FileExtension,
            d.FileSize,
            d.Status,
            d.ProcessingStage,
            d.EmbeddingStatus,
            d.PageCount,
            d.ChunkCount,
            d.Category,
            d.UploadedAt
        )).ToList();

        return Ok(new PaginatedList<DocumentSummaryDto>(dtos, totalCount, page, pageSize));
    }

    /// <summary>
    /// Get document detail by public DocumentId.
    /// </summary>
    [HttpGet("documents/{documentId}")]
    [ProducesResponseType(typeof(DocumentDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDocumentById(string documentId, CancellationToken cancellationToken = default)
    {
        var doc = await _documentRepository.GetByDocumentIdAsync(documentId, cancellationToken);
        if (doc == null || doc.Status == DocumentStatus.Deleted)
        {
            return NotFound(new { error = $"Document '{documentId}' was not found." });
        }

        var response = new DocumentDetailResponse(
            doc.DocumentId,
            doc.Title,
            doc.FileName,
            doc.OriginalFileName,
            doc.FileExtension,
            doc.MimeType,
            doc.FileSize,
            doc.FileHash,
            doc.Status,
            doc.ProcessingStage,
            doc.ProcessingError,
            doc.EmbeddingStatus,
            doc.EmbeddingModel,
            doc.EmbeddingError,
            doc.EmbeddedChunkCount,
            doc.IndexedAt,
            doc.PageCount,
            doc.CharacterCount,
            doc.WordCount,
            doc.ChunkCount,
            doc.Category,
            doc.Source,
            doc.Language,
            doc.Version,
            doc.Description,
            doc.UploadedBy,
            doc.UploadedAt,
            doc.ProcessedAt,
            doc.UpdatedAt
        );

        return Ok(response);
    }

    /// <summary>
    /// Get generated document chunks by DocumentId.
    /// </summary>
    [HttpGet("documents/{documentId}/chunks")]
    [ProducesResponseType(typeof(PaginatedList<DocumentChunkDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDocumentChunks(
        string documentId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var doc = await _documentRepository.GetByDocumentIdAsync(documentId, cancellationToken);
        if (doc == null || doc.Status == DocumentStatus.Deleted)
        {
            return NotFound(new { error = $"Document '{documentId}' was not found." });
        }

        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var (chunks, totalCount) = await _chunkRepository.GetByDocumentIdAsync(documentId, page, pageSize, cancellationToken);
        var dtos = chunks.Select(c => new DocumentChunkDto(
            c.ChunkId,
            c.DocumentId,
            c.ChunkIndex,
            c.Content,
            c.ContentHash,
            c.PageNumber,
            c.StartPage,
            c.EndPage,
            c.SectionTitle,
            c.HeadingPath,
            c.CharacterCount,
            c.WordCount,
            c.TokenEstimate,
            c.CreatedAt
        )).ToList();

        return Ok(new PaginatedList<DocumentChunkDto>(dtos, totalCount, page, pageSize));
    }

    /// <summary>
    /// View full extracted normalized text of document.
    /// </summary>
    [HttpGet("documents/{documentId}/content")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDocumentContent(string documentId, CancellationToken cancellationToken = default)
    {
        var doc = await _documentRepository.GetByDocumentIdAsync(documentId, cancellationToken);
        if (doc == null || doc.Status == DocumentStatus.Deleted)
        {
            return NotFound(new { error = $"Document '{documentId}' was not found." });
        }

        var (chunks, _) = await _chunkRepository.GetByDocumentIdAsync(documentId, page: 1, pageSize: 1000, cancellationToken);
        var combinedText = string.Join("\n\n---\n\n", chunks.Select(c => c.Content));

        return Ok(new
        {
            documentId = doc.DocumentId,
            title = doc.Title,
            fileName = doc.FileName,
            characterCount = doc.CharacterCount,
            wordCount = doc.WordCount,
            chunkCount = doc.ChunkCount,
            content = combinedText
        });
    }

    /// <summary>
    /// Reprocess document and regenerate chunks & vector embeddings.
    /// </summary>
    [HttpPost("documents/{documentId}/reprocess")]
    [ProducesResponseType(typeof(DocumentDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReprocessDocument(string documentId, CancellationToken cancellationToken = default)
    {
        try
        {
            await _vectorStore.DeleteDocumentAsync(documentId, cancellationToken);
            var doc = await _ingestionService.ReprocessAsync(documentId, cancellationToken);
            await _indexingService.IndexDocumentAsync(documentId, cancellationToken);

            return Ok(new DocumentDetailResponse(
                doc.DocumentId,
                doc.Title,
                doc.FileName,
                doc.OriginalFileName,
                doc.FileExtension,
                doc.MimeType,
                doc.FileSize,
                doc.FileHash,
                doc.Status,
                doc.ProcessingStage,
                doc.ProcessingError,
                doc.EmbeddingStatus,
                doc.EmbeddingModel,
                doc.EmbeddingError,
                doc.EmbeddedChunkCount,
                doc.IndexedAt,
                doc.PageCount,
                doc.CharacterCount,
                doc.WordCount,
                doc.ChunkCount,
                doc.Category,
                doc.Source,
                doc.Language,
                doc.Version,
                doc.Description,
                doc.UploadedBy,
                doc.UploadedAt,
                doc.ProcessedAt,
                doc.UpdatedAt
            ));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Re-index vector embeddings for a specific document.
    /// </summary>
    [HttpPost("documents/{documentId}/reindex")]
    [ProducesResponseType(typeof(DocumentDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReindexDocument(string documentId, CancellationToken cancellationToken = default)
    {
        try
        {
            var doc = await _indexingService.IndexDocumentAsync(documentId, cancellationToken);
            return Ok(new DocumentDetailResponse(
                doc.DocumentId,
                doc.Title,
                doc.FileName,
                doc.OriginalFileName,
                doc.FileExtension,
                doc.MimeType,
                doc.FileSize,
                doc.FileHash,
                doc.Status,
                doc.ProcessingStage,
                doc.ProcessingError,
                doc.EmbeddingStatus,
                doc.EmbeddingModel,
                doc.EmbeddingError,
                doc.EmbeddedChunkCount,
                doc.IndexedAt,
                doc.PageCount,
                doc.CharacterCount,
                doc.WordCount,
                doc.ChunkCount,
                doc.Category,
                doc.Source,
                doc.Language,
                doc.Version,
                doc.Description,
                doc.UploadedBy,
                doc.UploadedAt,
                doc.ProcessedAt,
                doc.UpdatedAt
            ));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Batch queue all unindexed documents for vector indexing.
    /// </summary>
    [HttpPost("documents/index-unindexed")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> IndexUnindexed(CancellationToken cancellationToken = default)
    {
        var indexedCount = await _indexingService.IndexUnindexedDocumentsAsync(cancellationToken);
        return Ok(new { message = $"Batch indexing triggered. {indexedCount} documents indexed." });
    }

    /// <summary>
    /// Delete document, chunks, and physical storage files and Qdrant vector points.
    /// </summary>
    [HttpDelete("documents/{documentId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteDocument(string documentId, CancellationToken cancellationToken = default)
    {
        var doc = await _documentRepository.GetByDocumentIdAsync(documentId, cancellationToken);
        if (doc == null || doc.Status == DocumentStatus.Deleted)
        {
            return NotFound(new { error = $"Document '{documentId}' was not found." });
        }

        await _vectorStore.DeleteDocumentAsync(documentId, cancellationToken);
        await _chunkRepository.DeleteByDocumentIdAsync(documentId, cancellationToken);
        await _documentRepository.DeleteAsync(documentId, cancellationToken);
        await _storage.DeleteAsync(documentId, cancellationToken);

        return Ok(new { message = $"Document '{doc.Title}', metadata, chunks, and Qdrant vector points were successfully deleted." });
    }

    private void QueueDocumentIndexing(string documentId)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var indexingService = scope.ServiceProvider.GetRequiredService<IKnowledgeIndexingService>();
                await indexingService.IndexDocumentAsync(documentId, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Background vector indexing failed for document {DocumentId}.", documentId);
            }
        }, CancellationToken.None);
    }
}
