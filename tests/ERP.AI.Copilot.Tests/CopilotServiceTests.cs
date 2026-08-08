using ERP.AI.Copilot.Providers;
using ERP.AI.Copilot.Services;
using ERP.AI.Core.Dtos;
using ERP.AI.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ERP.AI.Copilot.Tests;

public class CopilotServiceTests
{
    private readonly Mock<IErpToolRegistry> _mockRegistry = new();
    private readonly ILlmProvider _fakeLlmProvider = new FakeLlmProvider();

    [Fact]
    public async Task ProcessMessageAsync_Should_Return_Refusal_On_Write_Operation()
    {
        // Arrange
        var service = new CopilotService(_fakeLlmProvider, _mockRegistry.Object, NullLogger<CopilotService>.Instance);
        var request = new CopilotChatRequest { Message = "Tạo invoice mới cho MAEDA" };

        // Act
        var response = await service.ProcessMessageAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.Answer.Should().Contain("Phase 1 currently supports read-only ERP queries");
        response.ToolsUsed.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessMessageAsync_Should_Execute_Tool_And_Return_Answer_For_Top_Debtors()
    {
        // Arrange
        var mockTool = new Mock<IErpTool>();
        mockTool.Setup(t => t.Name).Returns("GetTopDebtors");
        mockTool.Setup(t => t.Description).Returns("Top Debtors Tool");

        _mockRegistry.Setup(r => r.GetAllTools()).Returns(new List<IErpTool> { mockTool.Object });
        _mockRegistry
            .Setup(r => r.ExecuteToolAsync("GetTopDebtors", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, new TopDebtorsOutput { TotalReceivable = 850000000m }, null));

        var service = new CopilotService(_fakeLlmProvider, _mockRegistry.Object, NullLogger<CopilotService>.Instance);
        var request = new CopilotChatRequest { Message = "Top 5 khách hàng đang nợ nhiều nhất?" };

        // Act
        var response = await service.ProcessMessageAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.ToolsUsed.Should().Contain("GetTopDebtors");
        response.TraceDetails.Should().ContainSingle(t => t.ToolName == "GetTopDebtors" && t.Success);
    }
}
