using ERP.AI.Infrastructure.Data;
using ERP.AI.Knowledge.Entities;
using ERP.AI.Knowledge.Enums;
using ERP.AI.Knowledge.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.AI.Infrastructure.Repositories;

public class KnowledgeDocumentRepository : IKnowledgeDocumentRepository
{
    private readonly ErpDbContext _dbContext;

    public KnowledgeDocumentRepository(ErpDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<KnowledgeDocument> CreateAsync(KnowledgeDocument document, CancellationToken cancellationToken = default)
    {
        await _dbContext.KnowledgeDocuments.AddAsync(document, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return document;
    }

    public async Task<KnowledgeDocument?> GetByDocumentIdAsync(string documentId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.KnowledgeDocuments
            .FirstOrDefaultAsync(d => d.DocumentId == documentId, cancellationToken);
    }

    public async Task<KnowledgeDocument?> GetByHashAsync(string fileHash, CancellationToken cancellationToken = default)
    {
        return await _dbContext.KnowledgeDocuments
            .FirstOrDefaultAsync(d => d.FileHash == fileHash, cancellationToken);
    }

    public async Task<(IReadOnlyList<KnowledgeDocument> Items, int TotalCount)> ListAsync(
        int page = 1,
        int pageSize = 20,
        DocumentStatus? status = null,
        string? category = null,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.KnowledgeDocuments.AsNoTracking().AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(d => d.Status == status.Value);
        }
        else
        {
            query = query.Where(d => d.Status != DocumentStatus.Deleted);
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(d => d.Category == category);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.Trim().ToLower();
            query = query.Where(d =>
                d.Title.ToLower().Contains(searchLower) ||
                d.FileName.ToLower().Contains(searchLower) ||
                (d.Description != null && d.Description.ToLower().Contains(searchLower)) ||
                (d.Source != null && d.Source.ToLower().Contains(searchLower)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(d => d.UploadedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<IReadOnlyList<KnowledgeDocument>> GetUnindexedAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.KnowledgeDocuments
            .Where(d => d.Status == DocumentStatus.Processed &&
                        (d.EmbeddingStatus == EmbeddingStatus.NotIndexed ||
                         d.EmbeddingStatus == EmbeddingStatus.Outdated ||
                         d.EmbeddingStatus == EmbeddingStatus.Failed))
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateAsync(KnowledgeDocument document, CancellationToken cancellationToken = default)
    {
        _dbContext.KnowledgeDocuments.Update(document);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(string documentId, CancellationToken cancellationToken = default)
    {
        var document = await GetByDocumentIdAsync(documentId, cancellationToken);
        if (document != null)
        {
            _dbContext.KnowledgeDocuments.Remove(document);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
