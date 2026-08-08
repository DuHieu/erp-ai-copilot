using ERP.AI.Core.Dtos;
using ERP.AI.Core.Interfaces;
using ERP.AI.Tools.Base;

namespace ERP.AI.Tools.Definitions;

public class GetProjectBudgetAlertsTool : ErpToolBase<EmptyInput, ProjectBudgetAlertsOutput>
{
    private readonly IProjectRepository _projectRepository;

    public GetProjectBudgetAlertsTool(IProjectRepository projectRepository)
    {
        _projectRepository = projectRepository;
    }

    public override string Name => "GetProjectBudgetAlerts";

    public override string Description =>
        "Retrieves projects where actual expenditure/cost exceeds the budgeted amount (over-budget alert).";

    public override string RequiredPermission => "Project.View";

    public override string ParameterJsonSchema => """
    {
      "type": "object",
      "properties": {}
    }
    """;

    protected override async Task<ProjectBudgetAlertsOutput> ExecuteCoreAsync(EmptyInput input, CancellationToken cancellationToken)
    {
        return await _projectRepository.GetProjectBudgetAlertsAsync(cancellationToken);
    }
}
