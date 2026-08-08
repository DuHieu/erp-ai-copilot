using ERP.AI.Core.Dtos;
using ERP.AI.Core.Entities;

namespace ERP.AI.Core.Interfaces;

public interface ICustomerRepository
{
    Task<Customer?> GetByCodeAsync(string customerCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Customer>> GetAllActiveAsync(CancellationToken cancellationToken = default);
}

public interface IInvoiceRepository
{
    Task<TopDebtorsOutput> GetTopDebtorsAsync(int top, DateTime? asOfDate, CancellationToken cancellationToken = default);
    Task<CustomerReceivableOutput?> GetCustomerReceivablesAsync(string customerCode, CancellationToken cancellationToken = default);
}

public interface ISalesRepository
{
    Task<RevenueSummaryOutput> GetRevenueSummaryAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);
}

public interface IInventoryRepository
{
    Task<InventoryAlertsOutput> GetInventoryAlertsAsync(CancellationToken cancellationToken = default);
}

public interface IProjectRepository
{
    Task<ProjectBudgetAlertsOutput> GetProjectBudgetAlertsAsync(CancellationToken cancellationToken = default);
}
