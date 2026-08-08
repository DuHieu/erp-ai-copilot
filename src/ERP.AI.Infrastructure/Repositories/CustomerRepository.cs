using ERP.AI.Core.Entities;
using ERP.AI.Core.Interfaces;
using ERP.AI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ERP.AI.Infrastructure.Repositories;

public class CustomerRepository : ICustomerRepository
{
    private readonly ErpDbContext _context;

    public CustomerRepository(ErpDbContext context)
    {
        _context = context;
    }

    public async Task<Customer?> GetByCodeAsync(string customerCode, CancellationToken cancellationToken = default)
    {
        return await _context.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CustomerCode.ToLower() == customerCode.ToLower(), cancellationToken);
    }

    public async Task<IReadOnlyList<Customer>> GetAllActiveAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Customers
            .AsNoTracking()
            .Where(c => c.IsActive)
            .ToListAsync(cancellationToken);
    }
}
