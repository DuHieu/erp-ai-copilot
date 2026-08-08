using ERP.AI.Core.Dtos;
using ERP.AI.Core.Enums;
using ERP.AI.Core.Interfaces;
using ERP.AI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ERP.AI.Infrastructure.Repositories;

public class InvoiceRepository : IInvoiceRepository
{
    private readonly ErpDbContext _context;

    public InvoiceRepository(ErpDbContext context)
    {
        _context = context;
    }

    public async Task<TopDebtorsOutput> GetTopDebtorsAsync(int top, DateTime? asOfDate, CancellationToken cancellationToken = default)
    {
        var targetDate = asOfDate ?? new DateTime(2026, 8, 9);

        // Fetch unpaid invoices with customer info
        var unpaidInvoices = await _context.Invoices
            .AsNoTracking()
            .Include(i => i.Customer)
            .Where(i => i.TotalAmount > i.PaidAmount && i.Customer != null && i.Customer.IsActive)
            .ToListAsync(cancellationToken);

        var grouped = unpaidInvoices
            .GroupBy(i => new { i.Customer!.CustomerCode, i.Customer.CustomerName })
            .Select(g =>
            {
                decimal totalRem = g.Sum(x => x.RemainingAmount);
                decimal overdueRem = g.Where(x => x.DueDate < targetDate).Sum(x => x.RemainingAmount);
                int count = g.Count();

                return new DebtorCustomerDto
                {
                    CustomerCode = g.Key.CustomerCode,
                    CustomerName = g.Key.CustomerName,
                    RemainingAmount = totalRem,
                    OverdueAmount = overdueRem,
                    InvoiceCount = count
                };
            })
            .Where(d => d.RemainingAmount > 0)
            .OrderByDescending(d => d.RemainingAmount)
            .Take(top > 0 ? top : 5)
            .ToList();

        decimal totalReceivable = grouped.Sum(x => x.RemainingAmount);

        return new TopDebtorsOutput
        {
            TotalReceivable = totalReceivable,
            Customers = grouped
        };
    }

    public async Task<CustomerReceivableOutput?> GetCustomerReceivablesAsync(string customerCode, CancellationToken cancellationToken = default)
    {
        var customer = await _context.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CustomerCode.ToLower() == customerCode.ToLower(), cancellationToken);

        if (customer == null)
        {
            return null;
        }

        var today = new DateTime(2026, 8, 9);
        var invoices = await _context.Invoices
            .AsNoTracking()
            .Where(i => i.CustomerId == customer.Id && i.TotalAmount > i.PaidAmount)
            .OrderBy(i => i.DueDate)
            .ToListAsync(cancellationToken);

        var invoiceDtos = invoices.Select(i =>
        {
            int daysOverdue = 0;
            if (i.DueDate < today)
            {
                daysOverdue = (today - i.DueDate).Days;
            }

            return new CustomerInvoiceDto
            {
                InvoiceNo = i.InvoiceNo,
                InvoiceDate = i.InvoiceDate,
                DueDate = i.DueDate,
                TotalAmount = i.TotalAmount,
                PaidAmount = i.PaidAmount,
                RemainingAmount = i.RemainingAmount,
                DaysOverdue = daysOverdue,
                Status = i.Status.ToString()
            };
        }).ToList();

        decimal totalReceivable = invoiceDtos.Sum(i => i.RemainingAmount);
        decimal overdueAmount = invoiceDtos.Where(i => i.DaysOverdue > 0).Sum(i => i.RemainingAmount);

        return new CustomerReceivableOutput
        {
            CustomerCode = customer.CustomerCode,
            CustomerName = customer.CustomerName,
            TotalReceivable = totalReceivable,
            OverdueAmount = overdueAmount,
            Invoices = invoiceDtos
        };
    }
}
