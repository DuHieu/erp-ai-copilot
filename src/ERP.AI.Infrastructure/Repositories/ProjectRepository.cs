using ERP.AI.Core.Dtos;
using ERP.AI.Core.Interfaces;
using ERP.AI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ERP.AI.Infrastructure.Repositories;

public class ProjectRepository : IProjectRepository
{
    private readonly ErpDbContext _context;

    public ProjectRepository(ErpDbContext context)
    {
        _context = context;
    }

    public async Task<ProjectBudgetAlertsOutput> GetProjectBudgetAlertsAsync(CancellationToken cancellationToken = default)
    {
        var overBudgetProjects = await _context.Projects
            .AsNoTracking()
            .Where(p => p.ActualCost > p.BudgetAmount)
            .OrderByDescending(p => p.ActualCost - p.BudgetAmount)
            .Select(p => new ProjectBudgetAlertDto
            {
                ProjectCode = p.ProjectCode,
                ProjectName = p.ProjectName,
                Budget = p.BudgetAmount,
                Actual = p.ActualCost,
                Variance = p.ActualCost - p.BudgetAmount,
                VariancePercent = p.BudgetAmount > 0
                    ? Math.Round((double)((p.ActualCost - p.BudgetAmount) / p.BudgetAmount) * 100, 2)
                    : 0
            })
            .ToListAsync(cancellationToken);

        return new ProjectBudgetAlertsOutput
        {
            Projects = overBudgetProjects
        };
    }
}
