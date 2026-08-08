using System.Text.Json.Serialization;

namespace ERP.AI.Core.Dtos;

// Tool 01: GetTopDebtors
public class TopDebtorsInput
{
    [JsonPropertyName("top")]
    public int Top { get; set; } = 5;

    [JsonPropertyName("asOfDate")]
    public DateTime? AsOfDate { get; set; }
}

public class DebtorCustomerDto
{
    [JsonPropertyName("customerCode")]
    public string CustomerCode { get; set; } = string.Empty;

    [JsonPropertyName("customerName")]
    public string CustomerName { get; set; } = string.Empty;

    [JsonPropertyName("remainingAmount")]
    public decimal RemainingAmount { get; set; }

    [JsonPropertyName("overdueAmount")]
    public decimal OverdueAmount { get; set; }

    [JsonPropertyName("invoiceCount")]
    public int InvoiceCount { get; set; }
}

public class TopDebtorsOutput
{
    [JsonPropertyName("totalReceivable")]
    public decimal TotalReceivable { get; set; }

    [JsonPropertyName("customers")]
    public List<DebtorCustomerDto> Customers { get; set; } = new();
}

// Tool 02: GetCustomerReceivable
public class CustomerReceivableInput
{
    [JsonPropertyName("customerCode")]
    public string CustomerCode { get; set; } = string.Empty;
}

public class CustomerInvoiceDto
{
    [JsonPropertyName("invoiceNo")]
    public string InvoiceNo { get; set; } = string.Empty;

    [JsonPropertyName("invoiceDate")]
    public DateTime InvoiceDate { get; set; }

    [JsonPropertyName("dueDate")]
    public DateTime DueDate { get; set; }

    [JsonPropertyName("totalAmount")]
    public decimal TotalAmount { get; set; }

    [JsonPropertyName("paidAmount")]
    public decimal PaidAmount { get; set; }

    [JsonPropertyName("remainingAmount")]
    public decimal RemainingAmount { get; set; }

    [JsonPropertyName("daysOverdue")]
    public int DaysOverdue { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;
}

public class CustomerReceivableOutput
{
    [JsonPropertyName("customerCode")]
    public string CustomerCode { get; set; } = string.Empty;

    [JsonPropertyName("customerName")]
    public string CustomerName { get; set; } = string.Empty;

    [JsonPropertyName("totalReceivable")]
    public decimal TotalReceivable { get; set; }

    [JsonPropertyName("overdueAmount")]
    public decimal OverdueAmount { get; set; }

    [JsonPropertyName("invoices")]
    public List<CustomerInvoiceDto> Invoices { get; set; } = new();
}

// Tool 03: GetRevenueSummary
public class RevenueSummaryInput
{
    [JsonPropertyName("from")]
    public DateTime From { get; set; }

    [JsonPropertyName("to")]
    public DateTime To { get; set; }
}

public class RevenueSummaryOutput
{
    [JsonPropertyName("from")]
    public string From { get; set; } = string.Empty;

    [JsonPropertyName("to")]
    public string To { get; set; } = string.Empty;

    [JsonPropertyName("revenue")]
    public decimal Revenue { get; set; }

    [JsonPropertyName("transactionCount")]
    public int TransactionCount { get; set; }

    [JsonPropertyName("previousPeriodRevenue")]
    public decimal PreviousPeriodRevenue { get; set; }

    [JsonPropertyName("changeAmount")]
    public decimal ChangeAmount { get; set; }

    [JsonPropertyName("changePercent")]
    public double ChangePercent { get; set; }
}

// Tool 04: GetInventoryAlerts
public class InventoryAlertItemDto
{
    [JsonPropertyName("itemCode")]
    public string ItemCode { get; set; } = string.Empty;

    [JsonPropertyName("itemName")]
    public string ItemName { get; set; } = string.Empty;

    [JsonPropertyName("unit")]
    public string Unit { get; set; } = string.Empty;

    [JsonPropertyName("currentStock")]
    public decimal CurrentStock { get; set; }

    [JsonPropertyName("minimumStock")]
    public decimal MinimumStock { get; set; }

    [JsonPropertyName("shortage")]
    public decimal Shortage { get; set; }
}

public class InventoryAlertsOutput
{
    [JsonPropertyName("items")]
    public List<InventoryAlertItemDto> Items { get; set; } = new();
}

// Tool 05: GetProjectBudgetAlerts
public class ProjectBudgetAlertDto
{
    [JsonPropertyName("projectCode")]
    public string ProjectCode { get; set; } = string.Empty;

    [JsonPropertyName("projectName")]
    public string ProjectName { get; set; } = string.Empty;

    [JsonPropertyName("budget")]
    public decimal Budget { get; set; }

    [JsonPropertyName("actual")]
    public decimal Actual { get; set; }

    [JsonPropertyName("variance")]
    public decimal Variance { get; set; }

    [JsonPropertyName("variancePercent")]
    public double VariancePercent { get; set; }
}

public class ProjectBudgetAlertsOutput
{
    [JsonPropertyName("projects")]
    public List<ProjectBudgetAlertDto> Projects { get; set; } = new();
}
