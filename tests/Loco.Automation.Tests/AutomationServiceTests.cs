using System.Threading.Tasks;
using Loco.Automation.Services;
using Loco.Core.Interfaces;
using Moq;
using Xunit;
using Microsoft.Extensions.Logging.Abstractions;
using Loco.Automation.Interfaces;

namespace Loco.Automation.Tests;

public class AutomationServiceTests
{
    private readonly Mock<IAutomationRuleEngine> _engineMock;
    private readonly Mock<IRuleStore> _storeMock;
    private readonly AutomationService _service;

    public AutomationServiceTests()
    {
        _engineMock = new Mock<IAutomationRuleEngine>(MockBehavior.Strict);
        _storeMock = new Mock<IRuleStore>(MockBehavior.Strict);

        // Default behaviors for tests that don't touch engine/store
        _engineMock.Setup(e => e.Dispose());

        _service = new AutomationService(new NullLogger<AutomationService>(), _engineMock.Object, _storeMock.Object);
    }

    [Fact]
    public async Task StartAsync_Should_LoadRulesAndStart()
    {
        // Arrange
        var rules = new[] { "{\"id\":\"r1\",\"name\":\"Test Rule\"}" };
        _storeMock.Setup(s => s.LoadAllRulesAsync(It.IsAny<System.Threading.CancellationToken>()))
                  .ReturnsAsync(rules);
        _engineMock.Setup(e => e.AddRuleAsync(It.IsAny<Loco.Core.Models.AutomationDsl.Rule>(), It.IsAny<System.Threading.CancellationToken>()))
                   .ReturnsAsync(true);

        // Act
        var result = await _service.StartAsync();

        // Assert
        Assert.True(result);
        _storeMock.Verify(s => s.LoadAllRulesAsync(It.IsAny<System.Threading.CancellationToken>()), Times.Once);
        _engineMock.Verify(e => e.AddRuleAsync(It.IsAny<Loco.Core.Models.AutomationDsl.Rule>(), It.IsAny<System.Threading.CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StopAsync_Should_Stop_Successfully()
    {
        // Arrange
        _storeMock.Setup(s => s.LoadAllRulesAsync(It.IsAny<System.Threading.CancellationToken>()))
                  .ReturnsAsync(new string[0]);

        // Act
        await _service.StartAsync();
        var result = await _service.StopAsync();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task RegisterFlowAsync_NewFlow_ShouldSucceed()
    {
        var flowMock = new Mock<IFlow>();
        flowMock.Setup(f => f.Id).Returns("flow1");

        var result = await _service.RegisterFlowAsync(flowMock.Object);

        Assert.True(result);
        var activeFlows = await _service.GetActiveFlowsAsync();
        Assert.Contains(flowMock.Object, activeFlows);
    }

    [Fact]
    public async Task RegisterFlowAsync_ExistingFlow_ShouldFail()
    {
        var flowMock = new Mock<IFlow>();
        flowMock.Setup(f => f.Id).Returns("flow1");
        await _service.RegisterFlowAsync(flowMock.Object);

        var result = await _service.RegisterFlowAsync(flowMock.Object);

        Assert.False(result);
    }

    [Fact]
    public async Task UnregisterFlowAsync_ExistingFlow_ShouldSucceed()
    {
        var flowMock = new Mock<IFlow>();
        flowMock.Setup(f => f.Id).Returns("flow1");
        await _service.RegisterFlowAsync(flowMock.Object);

        var result = await _service.UnregisterFlowAsync("flow1");

        Assert.True(result);
        var activeFlows = await _service.GetActiveFlowsAsync();
        Assert.DoesNotContain(flowMock.Object, activeFlows);
    }

    [Fact]
    public async Task UnregisterFlowAsync_NonExistingFlow_ShouldFail()
    {
        var result = await _service.UnregisterFlowAsync("non_existent_flow");
        Assert.False(result);
    }

    [Fact]
    public async Task AddRuleFromJsonAsync_ValidRule_ShouldSucceed()
    {
        var json = @"{\"id\":\"r1\",\"name\":\"Test Rule\"}";

        _engineMock
            .Setup(e => e.AddRuleAsync(It.IsAny<Loco.Core.Models.AutomationDsl.Rule>(), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(true);
        _storeMock
            .Setup(s => s.SaveRuleAsync("r1", json, It.IsAny<System.Threading.CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _service.AddRuleFromJsonAsync(json);
        Assert.True(result);
    }

    [Fact]
    public async Task AddRuleFromJsonAsync_InvalidRule_ShouldFail()
    {
        var json = @"{\"name\":\"Invalid Rule without ID\"}";
        var result = await _service.AddRuleFromJsonAsync(json);
        Assert.False(result);
    }

    [Fact]
    public async Task Validate_ValidRule_Ok()
    {
        var json = @"{\"id\":\"r1\",\"name\":\"Test\",\"enabled\":true}";
        var res = await _service.ValidateRuleJsonAsync(json);
        Assert.True(res.IsValid);
        Assert.Empty(res.Errors);
    }

    [Fact]
    public async Task Validate_MissingRequired_Fails()
    {
        var json = @"{\"name\":\"Test only\"}"; // missing id
        var res = await _service.ValidateRuleJsonAsync(json);
        Assert.False(res.IsValid);
        Assert.Single(res.Errors);
        Assert.Contains("'id' is required", res.Errors[0]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("invalid-json")]
    [InlineData("{")]
    public async Task Validate_InvalidJson_Fails(string json)
    {
        var res = await _service.ValidateRuleJsonAsync(json);
        Assert.False(res.IsValid);
        Assert.Single(res.Errors);
        Assert.Equal("Invalid JSON", res.Errors[0]);
    }

    [Fact]
    public async Task DeleteRuleAsync_ExistingRule_ShouldSucceed()
    {
        // Arrange
        var ruleId = "rule-to-delete";
        _engineMock.Setup(e => e.DeleteRuleAsync(ruleId))
                   .ReturnsAsync(true);
        _storeMock.Setup(s => s.DeleteRuleAsync(ruleId, It.IsAny<System.Threading.CancellationToken>()))
                  .Returns(Task.CompletedTask);

        // Act
        var result = await _service.DeleteRuleAsync(ruleId);

        // Assert
        Assert.True(result);
        _engineMock.Verify(e => e.DeleteRuleAsync(ruleId), Times.Once);
        _storeMock.Verify(s => s.DeleteRuleAsync(ruleId, It.IsAny<System.Threading.CancellationToken>()), Times.Once);
    }
}