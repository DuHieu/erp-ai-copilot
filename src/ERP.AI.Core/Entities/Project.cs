namespace ERP.AI.Core.Entities;

public class Project
{
    public int Id { get; set; }
    public string ProjectCode { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public decimal BudgetAmount { get; set; }
    public decimal ActualCost { get; set; }
    public string Status { get; set; } = "In Progress";
}
