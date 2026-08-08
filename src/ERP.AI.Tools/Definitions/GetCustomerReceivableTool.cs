using ERP.AI.Core.Dtos;
using ERP.AI.Core.Interfaces;
using ERP.AI.Tools.Base;

namespace ERP.AI.Tools.Definitions;

public class GetCustomerReceivableTool : ErpToolBase<CustomerReceivableInput, CustomerReceivableOutput>
{
    private readonly IInvoiceRepository _invoiceRepository;

    public GetCustomerReceivableTool(IInvoiceRepository invoiceRepository)
    {
        _invoiceRepository = invoiceRepository;
    }

    public override string Name => "GetCustomerReceivable";

    public override string Description =>
        "Retrieves detailed accounts receivable information and unpaid invoices for a specific customer by code (e.g., CUS001). Useful when asked how much a specific customer owes or for customer receivable status.";

    public override string RequiredPermission => "Accounting.View";

    public override string ParameterJsonSchema => """
    {
      "type": "object",
      "properties": {
        "customerCode": { "type": "string", "description": "Customer code, e.g. CUS001" }
      },
      "required": ["customerCode"]
    }
    """;

    protected override void ValidateInput(CustomerReceivableInput input)
    {
        if (string.IsNullOrWhiteSpace(input.CustomerCode))
        {
            throw new ArgumentException("Customer code cannot be empty.", nameof(input.CustomerCode));
        }
    }

    protected override async Task<CustomerReceivableOutput> ExecuteCoreAsync(CustomerReceivableInput input, CancellationToken cancellationToken)
    {
        var result = await _invoiceRepository.GetCustomerReceivablesAsync(input.CustomerCode, cancellationToken);
        return result ?? new CustomerReceivableOutput
        {
            CustomerCode = input.CustomerCode,
            CustomerName = "Not Found",
            TotalReceivable = 0,
            OverdueAmount = 0,
            Invoices = new List<CustomerInvoiceDto>()
        };
    }
}
