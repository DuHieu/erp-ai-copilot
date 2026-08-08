using ERP.AI.Core.Dtos;
using ERP.AI.Core.Interfaces;
using ERP.AI.Tools.Base;

namespace ERP.AI.Tools.Definitions;

public class GetInventoryAlertsTool : ErpToolBase<EmptyInput, InventoryAlertsOutput>
{
    private readonly IInventoryRepository _inventoryRepository;

    public GetInventoryAlertsTool(IInventoryRepository inventoryRepository)
    {
        _inventoryRepository = inventoryRepository;
    }

    public override string Name => "GetInventoryAlerts";

    public override string Description =>
        "Retrieves items that have reached or dropped below their minimum stock threshold (low stock / inventory shortage alert).";

    public override string RequiredPermission => "Inventory.View";

    public override string ParameterJsonSchema => """
    {
      "type": "object",
      "properties": {}
    }
    """;

    protected override async Task<InventoryAlertsOutput> ExecuteCoreAsync(EmptyInput input, CancellationToken cancellationToken)
    {
        return await _inventoryRepository.GetInventoryAlertsAsync(cancellationToken);
    }
}
