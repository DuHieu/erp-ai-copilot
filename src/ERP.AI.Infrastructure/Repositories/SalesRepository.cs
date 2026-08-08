using ERP.AI.Core.Dtos;
using ERP.AI.Core.Interfaces;
using ERP.AI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ERP.AI.Infrastructure.Repositories;

public class SalesRepository : ISalesRepository
{
    private readonly ErpDbContext _context;

    public SalesRepository(ErpDbContext context)
    {
        _context = context;
    }

    public async Task<RevenueSummaryOutput> GetRevenueSummaryAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
    {
        // Normalize range
        var start = fromDate.Date;
        var end = toDate.Date.AddDays(1).AddTicks(-1);

        var currentPeriodSales = await _context.Sales
            .AsNoTracking()
            .Where(s => s.TransactionDate >= start && s.TransactionDate <= end)
            .ToListAsync(cancellationToken);

        decimal currentRevenue = currentPeriodSales.Sum(s => s.Amount);
        int transactionCount = currentPeriodSales.Count;

        // Calculate previous period of equal length
        var periodSpan = end - start;
        var prevEnd = start.AddTicks(-1);
        var prevStart = prevEnd.Subtract(periodSpan);

        var prevPeriodSales = await _context.Sales
            .AsNoTracking()
            .Where(s => s.TransactionDate >= prevStart && s.TransactionDate <= prevEnd)
            .ToListAsync(cancellationToken);

        decimal previousRevenue = prevPeriodSales.Sum(s => s.Amount);
        decimal changeAmount = currentRevenue - previousRevenue;
        double changePercent = previousRevenue > 0
            ? (double)Math.Round((changeAmount / previousRevenue) * 100, 2)
            : 0;

        return new RevenueSummaryOutput
        {
            From = start.ToString("yyyy-MM-dd"),
            To = toDate.ToString("yyyy-MM-dd"),
            Revenue = currentRevenue,
            TransactionCount = transactionCount,
            PreviousPeriodRevenue = previousRevenue,
            ChangeAmount = changeAmount,
            ChangePercent = changePercent
        };
    }
}
