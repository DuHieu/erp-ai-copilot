using ERP.AI.Infrastructure.Data;
using ERP.AI.Knowledge.Entities;
using ERP.AI.Knowledge.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.AI.Infrastructure.Repositories;

public class KnowledgeChunkRepository : IKnowledgeChunkRepository
{
    private readonly ErpDbContext _dbContext;

    public KnowledgeChunkRepository(ErpDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddRangeAsync(IEnumerable<KnowledgeChunk> chunks, CancellationToken cancellationToken = default)
    {
        await _dbContext.KnowledgeChunks.AddRangeAsync(chunks, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<KnowledgeChunk> Items, int TotalCount)> GetByDocumentIdAsync(
        string documentId,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.KnowledgeChunks
            .AsNoTracking()
            .Where(c => c.DocumentId == documentId);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(c => c.ChunkIndex)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task DeleteByDocumentIdAsync(string documentId, CancellationToken cancellationToken = default)
    {
        var chunks = await _dbContext.KnowledgeChunks
            .Where(c => c.DocumentId == documentId)
            .ToListAsync(cancellationToken);

        if (chunks.Count > 0)
        {
            _dbContext.KnowledgeChunks.RemoveRange(chunks);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
