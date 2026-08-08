using ERP.AI.Knowledge.Dtos;
using ERP.AI.Knowledge.Entities;
using ERP.AI.Knowledge.Enums;
using ERP.AI.Knowledge.Interfaces;
using Microsoft.Extensions.Logging;

namespace ERP.AI.Knowledge.Services;

public class KnowledgeIndexingService : IKnowledgeIndexingService
{
    private readonly IKnowledgeDocumentRepository _documentRepository;
    private readonly IKnowledgeChunkRepository _chunkRepository;
    private readonly IEmbeddingService _embeddingService;
    private readonly IKnowledgeVectorStore _vectorStore;
    private readonly ILogger<KnowledgeIndexingService> _logger;

    public KnowledgeIndexingService(
        IKnowledgeDocumentRepository documentRepository,
        IKnowledgeChunkRepository chunkRepository,
        IEmbeddingService embeddingService,
        IKnowledgeVectorStore vectorStore,
        ILogger<KnowledgeIndexingService> logger)
    {
        _documentRepository = documentRepository;
        _chunkRepository = chunkRepository;
        _embeddingService = embeddingService;
        _vectorStore = vectorStore;
        _logger = logger;
    }

    public async Task<KnowledgeDocument> IndexDocumentAsync(string documentId, CancellationToken cancellationToken = default)
    {
        var doc = await _documentRepository.GetByDocumentIdAsync(documentId, cancellationToken);
        if (doc == null || doc.Status == DocumentStatus.Deleted)
        {
            throw new KeyNotFoundException($"Document with Id '{documentId}' was not found.");
        }

        if (doc.Status != DocumentStatus.Processed)
        {
            throw new InvalidOperationException($"Document '{doc.Title}' cannot be indexed because it is in status '{doc.Status}'. Only Processed documents can be indexed.");
        }

        _logger.LogInformation("Starting vector indexing for document {DocumentId} ('{Title}')", documentId, doc.Title);

        doc.EmbeddingStatus = EmbeddingStatus.Indexing;
        doc.EmbeddingError = null;
        doc.UpdatedAt = DateTime.UtcNow;
        await _documentRepository.UpdateAsync(doc, cancellationToken);

        try
        {
            var (chunks, _) = await _chunkRepository.GetByDocumentIdAsync(documentId, page: 1, pageSize: 10000, cancellationToken);
            if (chunks.Count == 0)
            {
                _logger.LogWarning("No chunks found in SQLite for document {DocumentId}.", documentId);
                doc.EmbeddingStatus = EmbeddingStatus.Indexed;
                doc.EmbeddedChunkCount = 0;
                doc.IndexedAt = DateTime.UtcNow;
                await _documentRepository.UpdateAsync(doc, cancellationToken);
                return doc;
            }

            var chunkTexts = chunks.Select(c => c.Content).ToList();
            var embeddings = await _embeddingService.EmbedDocumentsAsync(chunkTexts, cancellationToken);

            if (embeddings.Count == 0)
            {
                throw new InvalidOperationException("Failed to generate vector embeddings.");
            }

            var firstEmbedding = embeddings[0];
            await _vectorStore.EnsureCollectionAsync(firstEmbedding.Dimension, "Cosine", cancellationToken);

            // Clean previous vectors if re-indexing
            await _vectorStore.DeleteDocumentAsync(documentId, cancellationToken);

            var vectorPoints = new List<VectorPoint>();
            for (int i = 0; i < chunks.Count; i++)
            {
                var chunk = chunks[i];
                var emb = embeddings[i];

                var payload = new Dictionary<string, object>
                {
                    { "chunkId", chunk.ChunkId },
                    { "documentId", doc.DocumentId },
                    { "chunkIndex", chunk.ChunkIndex },
                    { "documentTitle", doc.Title },
                    { "fileName", doc.FileName },
                    { "content", chunk.Content },
                    { "contentHash", chunk.ContentHash },
                    { "category", doc.Category ?? "General" },
                    { "language", doc.Language ?? "Auto" },
                    { "source", doc.Source ?? string.Empty },
                    { "version", doc.Version ?? string.Empty },
                    { "sectionTitle", chunk.SectionTitle ?? string.Empty },
                    { "headingPath", chunk.HeadingPath ?? string.Empty },
                    { "startPage", chunk.StartPage ?? 1 },
                    { "endPage", chunk.EndPage ?? 1 },
                    { "wordCount", chunk.WordCount },
                    { "createdAt", chunk.CreatedAt.ToString("o") }
                };

                vectorPoints.Add(new VectorPoint(chunk.ChunkId, emb.Vector, payload));
            }

            await _vectorStore.UpsertAsync(vectorPoints, cancellationToken);

            doc.EmbeddingStatus = EmbeddingStatus.Indexed;
            doc.EmbeddingModel = firstEmbedding.Model;
            doc.EmbeddedChunkCount = chunks.Count;
            doc.IndexedAt = DateTime.UtcNow;
            doc.UpdatedAt = DateTime.UtcNow;

            await _documentRepository.UpdateAsync(doc, cancellationToken);
            _logger.LogInformation("Document {DocumentId} successfully indexed with {Count} vector points in Qdrant.", documentId, chunks.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to index document {DocumentId}", documentId);
            doc.EmbeddingStatus = EmbeddingStatus.Failed;
            doc.EmbeddingError = ex.Message;
            doc.UpdatedAt = DateTime.UtcNow;
            await _documentRepository.UpdateAsync(doc, cancellationToken);
        }

        return doc;
    }

    public async Task<int> IndexUnindexedDocumentsAsync(CancellationToken cancellationToken = default)
    {
        var unindexedDocs = await _documentRepository.GetUnindexedAsync(cancellationToken);
        int successCount = 0;

        foreach (var doc in unindexedDocs)
        {
            try
            {
                await IndexDocumentAsync(doc.DocumentId, cancellationToken);
                successCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error batch indexing unindexed document {DocumentId}", doc.DocumentId);
            }
        }

        return successCount;
    }

    public async Task RebuildIndexAsync(CancellationToken cancellationToken = default)
    {
        var (docs, _) = await _documentRepository.ListAsync(page: 1, pageSize: 10000, status: DocumentStatus.Processed, cancellationToken: cancellationToken);
        _logger.LogInformation("Rebuilding full vector index for {Count} processed documents...", docs.Count);

        foreach (var doc in docs)
        {
            try
            {
                await IndexDocumentAsync(doc.DocumentId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rebuilding index for document {DocumentId}", doc.DocumentId);
            }
        }
    }
}
