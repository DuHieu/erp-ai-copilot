using ERP.AI.Core.Dtos;
using ERP.AI.Core.Interfaces;
using ERP.AI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ERP.AI.Infrastructure.Repositories;

public class InventoryRepository : IInventoryRepository
{
    private readonly ErpDbContext _context;

    public InventoryRepository(ErpDbContext context)
    {
        _context = context;
    }

    public async Task<InventoryAlertsOutput> GetInventoryAlertsAsync(CancellationToken cancellationToken = default)
    {
        var lowStockItems = await _context.Items
            .AsNoTracking()
            .Where(i => i.CurrentStock <= i.MinimumStock)
            .OrderBy(i => i.CurrentStock - i.MinimumStock)
            .Select(i => new InventoryAlertItemDto
            {
                ItemCode = i.ItemCode,
                ItemName = i.ItemName,
                Unit = i.Unit,
                CurrentStock = i.CurrentStock,
                MinimumStock = i.MinimumStock,
                Shortage = i.MinimumStock - i.CurrentStock
            })
            .ToListAsync(cancellationToken);

        return new InventoryAlertsOutput
        {
            Items = lowStockItems
        };
    }
}
