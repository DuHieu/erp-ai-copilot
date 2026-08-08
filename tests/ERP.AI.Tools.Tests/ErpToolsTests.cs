using ERP.AI.Core.Dtos;
using ERP.AI.Core.Interfaces;
using ERP.AI.Tools.Definitions;
using FluentAssertions;
using Moq;
using Xunit;

namespace ERP.AI.Tools.Tests;

public class ErpToolsTests
{
    private readonly Mock<IInvoiceRepository> _mockInvoiceRepo = new();
    private readonly Mock<ISalesRepository> _mockSalesRepo = new();
    private readonly Mock<IInventoryRepository> _mockInventoryRepo = new();
    private readonly Mock<IProjectRepository> _mockProjectRepo = new();

    [Fact]
    public async Task GetTopDebtorsTool_Should_Return_Expected_Result()
    {
        // Arrange
        var expectedOutput = new TopDebtorsOutput
        {
            TotalReceivable = 1270000000m,
            Customers = new List<DebtorCustomerDto>
            {
                new() { CustomerCode = "CUS001", CustomerName = "MAEDA", RemainingAmount = 850000000m, OverdueAmount = 420000000m, InvoiceCount = 3 },
                new() { CustomerCode = "CUS002", CustomerName = "ABC", RemainingAmount = 420000000m, OverdueAmount = 200000000m, InvoiceCount = 2 }
            }
        };

        _mockInvoiceRepo
            .Setup(r => r.GetTopDebtorsAsync(5, It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedOutput);

        var tool = new GetTopDebtorsTool(_mockInvoiceRepo.Object);

        // Act
        var result = await tool.ExecuteAsync("""{"top": 5}""");

        // Assert
        result.Should().NotBeNull();
        var typedResult = result as TopDebtorsOutput;
        typedResult.Should().NotBeNull();
        typedResult!.TotalReceivable.Should().Be(1270000000m);
        typedResult.Customers.Should().HaveCount(2);
        typedResult.Customers[0].CustomerCode.Should().Be("CUS001");
    }

    [Fact]
    public async Task GetCustomerReceivableTool_Should_Throw_On_Empty_CustomerCode()
    {
        // Arrange
        var tool = new GetCustomerReceivableTool(_mockInvoiceRepo.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => tool.ExecuteAsync("""{"customerCode": ""}"""));
    }

    [Fact]
    public async Task GetCustomerReceivableTool_Should_Return_Customer_Details()
    {
        // Arrange
        var expected = new CustomerReceivableOutput
        {
            CustomerCode = "CUS001",
            CustomerName = "MAEDA",
            TotalReceivable = 850000000m,
            OverdueAmount = 420000000m,
            Invoices = new List<CustomerInvoiceDto>
            {
                new() { InvoiceNo = "INV-001", RemainingAmount = 350000000m, DaysOverdue = 30, Status = "Overdue" }
            }
        };

        _mockInvoiceRepo
            .Setup(r => r.GetCustomerReceivablesAsync("CUS001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var tool = new GetCustomerReceivableTool(_mockInvoiceRepo.Object);

        // Act
        var result = await tool.ExecuteAsync("""{"customerCode": "CUS001"}""");

        // Assert
        var typed = result as CustomerReceivableOutput;
        typed.Should().NotBeNull();
        typed!.CustomerCode.Should().Be("CUS001");
        typed.Invoices.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetRevenueSummaryTool_Should_Return_Revenue_Metrics()
    {
        // Arrange
        var expected = new RevenueSummaryOutput
        {
            From = "2026-07-01",
            To = "2026-07-31",
            Revenue = 12500000000m,
            TransactionCount = 158,
            PreviousPeriodRevenue = 10000000000m,
            ChangeAmount = 2500000000m,
            ChangePercent = 25.0
        };

        _mockSalesRepo
            .Setup(r => r.GetRevenueSummaryAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var tool = new GetRevenueSummaryTool(_mockSalesRepo.Object);

        // Act
        var result = await tool.ExecuteAsync("""{"from": "2026-07-01", "to": "2026-07-31"}""");

        // Assert
        var typed = result as RevenueSummaryOutput;
        typed.Should().NotBeNull();
        typed!.Revenue.Should().Be(12500000000m);
        typed.TransactionCount.Should().Be(158);
    }

    [Fact]
    public async Task GetInventoryAlertsTool_Should_Return_Low_Stock_Items()
    {
        // Arrange
        var expected = new InventoryAlertsOutput
        {
            Items = new List<InventoryAlertItemDto>
            {
                new() { ItemCode = "ITEM001", ItemName = "Steel Plate", CurrentStock = 80, MinimumStock = 100, Shortage = 20 }
            }
        };

        _mockInventoryRepo
            .Setup(r => r.GetInventoryAlertsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var tool = new GetInventoryAlertsTool(_mockInventoryRepo.Object);

        // Act
        var result = await tool.ExecuteAsync("{}");

        // Assert
        var typed = result as InventoryAlertsOutput;
        typed.Should().NotBeNull();
        typed!.Items.Should().HaveCount(1);
        typed.Items[0].ItemCode.Should().Be("ITEM001");
    }

    [Fact]
    public async Task GetProjectBudgetAlertsTool_Should_Return_Over_Budget_Projects()
    {
        // Arrange
        var expected = new ProjectBudgetAlertsOutput
        {
            Projects = new List<ProjectBudgetAlertDto>
            {
                new() { ProjectCode = "PRJ001", ProjectName = "MAEDA Factory", Budget = 10000000000m, Actual = 10820000000m, Variance = 820000000m, VariancePercent = 8.2 }
            }
        };

        _mockProjectRepo
            .Setup(r => r.GetProjectBudgetAlertsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var tool = new GetProjectBudgetAlertsTool(_mockProjectRepo.Object);

        // Act
        var result = await tool.ExecuteAsync("{}");

        // Assert
        var typed = result as ProjectBudgetAlertsOutput;
        typed.Should().NotBeNull();
        typed!.Projects.Should().HaveCount(1);
        typed.Projects[0].ProjectCode.Should().Be("PRJ001");
    }
}
