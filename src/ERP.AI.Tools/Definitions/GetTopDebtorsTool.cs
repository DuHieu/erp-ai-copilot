using ERP.AI.Core.Dtos;
using ERP.AI.Core.Interfaces;
using ERP.AI.Tools.Base;

namespace ERP.AI.Tools.Definitions;

public class GetTopDebtorsTool : ErpToolBase<TopDebtorsInput, TopDebtorsOutput>
{
    private readonly IInvoiceRepository _invoiceRepository;

    public GetTopDebtorsTool(IInvoiceRepository invoiceRepository)
    {
        _invoiceRepository = invoiceRepository;
    }

    public override string Name => "GetTopDebtors";

    public override string Description =>
        "Retrieves top debtor customers with the highest outstanding receivable balances. Useful when asked about top debtors, highest unpaid customers, or overdue balances.";

    public override string RequiredPermission => "Accounting.View";

    public override string ParameterJsonSchema => """
    {
      "type": "object",
      "properties": {
        "top": { "type": "integer", "description": "Number of top debtors to return (default: 5)" },
        "asOfDate": { "type": "string", "format": "date", "description": "As of date in YYYY-MM-DD format (optional)" }
      }
    }
    """;

    protected override void ValidateInput(TopDebtorsInput input)
    {
        if (input.Top <= 0)
        {
            input.Top = 5;
        }
        if (input.Top > 100)
        {
            input.Top = 100;
        }
    }

    protected override async Task<TopDebtorsOutput> ExecuteCoreAsync(TopDebtorsInput input, CancellationToken cancellationToken)
    {
        return await _invoiceRepository.GetTopDebtorsAsync(input.Top, input.AsOfDate, cancellationToken);
    }
}
