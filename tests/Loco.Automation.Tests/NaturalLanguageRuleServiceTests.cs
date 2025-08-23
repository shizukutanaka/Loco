using System.Threading.Tasks;
using Loco.Automation.Services;
using Loco.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Loco.Automation.Tests;

public class NaturalLanguageRuleServiceTests
{
    private readonly Mock<NaturalLanguageToDslConverter> _converterMock;
    private readonly NaturalLanguageRuleService _service;

    public NaturalLanguageRuleServiceTests()
    {
        _converterMock = new Mock<NaturalLanguageToDslConverter>();
        _service = new NaturalLanguageRuleService(new NullLogger<NaturalLanguageRuleService>(), _converterMock.Object);
    }

    [Fact]
    public async Task ConvertTextToRuleJsonAsync_SuccessfulConversion_ReturnsJson()
    {
        var rule = new AutomationRule { Id = "rule1", Name = "Test Rule" };
        var conversionResult = new ConversionResult { Success = true, Rules = new[] { rule } };
        _converterMock.Setup(c => c.ConvertAsync(It.IsAny<string>())).ReturnsAsync(conversionResult);

        var result = await _service.ConvertTextToRuleJsonAsync("some text");

        Assert.NotEmpty(result);
        Assert.Contains("\"id\":\"rule1\"", result);
    }

    [Fact]
    public async Task ConvertTextToRuleJsonAsync_FailedConversion_ReturnsEmptyString()
    {
        var conversionResult = new ConversionResult { Success = false };
        _converterMock.Setup(c => c.ConvertAsync(It.IsAny<string>())).ReturnsAsync(conversionResult);

        var result = await _service.ConvertTextToRuleJsonAsync("some text");

        Assert.Empty(result);
    }

    [Fact]
    public async Task ConvertTextToRuleJsonAsync_NullRules_ReturnsEmptyString()
    {
        var conversionResult = new ConversionResult { Success = true, Rules = null };
        _converterMock.Setup(c => c.ConvertAsync(It.IsAny<string>())).ReturnsAsync(conversionResult);

        var result = await _service.ConvertTextToRuleJsonAsync("some text");

        Assert.Empty(result);
    }

    [Fact]
    public async Task ConvertTextToRuleJsonAsync_EmptyRules_ReturnsEmptyString()
    {
        var conversionResult = new ConversionResult { Success = true, Rules = System.Array.Empty<AutomationRule>() };
        _converterMock.Setup(c => c.ConvertAsync(It.IsAny<string>())).ReturnsAsync(conversionResult);

        var result = await _service.ConvertTextToRuleJsonAsync("some text");

        Assert.Empty(result);
    }
}
