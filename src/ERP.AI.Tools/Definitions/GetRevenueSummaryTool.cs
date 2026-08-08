using ERP.AI.Core.Dtos;
using ERP.AI.Core.Interfaces;
using ERP.AI.Tools.Base;

namespace ERP.AI.Tools.Definitions;

public class GetRevenueSummaryTool : ErpToolBase<RevenueSummaryInput, RevenueSummaryOutput>
{
    private readonly ISalesRepository _salesRepository;

    public GetRevenueSummaryTool(ISalesRepository salesRepository)
    {
        _salesRepository = salesRepository;
    }

    public override string Name => "GetRevenueSummary";

    public override string Description =>
        "Retrieves sales revenue summary and transaction count for a specified date range (from, to in YYYY-MM-DD format). Useful for answering monthly/period revenue questions.";

    public override string RequiredPermission => "Sales.View";

    public override string ParameterJsonSchema => """
    {
      "type": "object",
      "properties": {
        "from": { "type": "string", "format": "date", "description": "Start date in YYYY-MM-DD format" },
        "to": { "type": "string", "format": "date", "description": "End date in YYYY-MM-DD format" }
      },
      "required": ["from", "to"]
    }
    """;

    protected override void ValidateInput(RevenueSummaryInput input)
    {
        if (input.From == default)
        {
            input.From = new DateTime(2026, 7, 1);
        }
        if (input.To == default)
        {
            input.To = new DateTime(2026, 7, 31);
        }
        if (input.From > input.To)
        {
            (input.From, input.To) = (input.To, input.From);
        }
    }

    protected override async Task<RevenueSummaryOutput> ExecuteCoreAsync(RevenueSummaryInput input, CancellationToken cancellationToken)
    {
        return await _salesRepository.GetRevenueSummaryAsync(input.From, input.To, cancellationToken);
    }
}
