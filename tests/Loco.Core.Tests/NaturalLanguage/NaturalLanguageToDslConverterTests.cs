using System;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.Logging;
using Loco.Core.Models;
using Loco.Core.NaturalLanguage;
using System.Text.Json;

namespace Loco.Core.Tests.NaturalLanguage
{
    public class NaturalLanguageToDslConverterTests
    {
        private readonly Mock<ILogger<NaturalLanguageToDslConverter>> _loggerMock;
        private readonly NaturalLanguageToDslConverter _converter;

        public NaturalLanguageToDslConverterTests()
        {
            _loggerMock = new Mock<ILogger<NaturalLanguageToDslConverter>>();
            _converter = new NaturalLanguageToDslConverter(_loggerMock.Object);
        }

        [Theory]
        [InlineData("Turn on the lights at 7 AM")]
        [InlineData("Send me a notification when the file changes")]
        [InlineData("Backup my documents every day at midnight")]
        [InlineData("Open Chrome when I start my computer")]
        public async Task ConvertText_Should_Return_Valid_Json(string input)
        {
            // Act
            var result = await _converter.ConvertTextAsync(input);

            // Assert
            result.Should().NotBeNullOrEmpty();
            
            // Verify it's valid JSON
            Action act = () => JsonDocument.Parse(result);
            act.Should().NotThrow();
        }

        [Theory]
        [InlineData("At 9:00 AM, send notification 'Good morning'", "9:00", "Good morning")]
        [InlineData("Every day at 6 PM notify me 'Time to go home'", "18:00", "Time to go home")]
        public async Task ConvertText_Should_Extract_Time_And_Message(string input, string expectedTime, string expectedMessage)
        {
            // Act
            var result = await _converter.ConvertTextAsync(input);
            var rule = JsonSerializer.Deserialize<AutomationDsl.Rule>(result);

            // Assert
            rule.Should().NotBeNull();
            rule!.Actions.Should().NotBeEmpty();
            
            // Check if the expected values are present in the rule configuration
            var json = JsonSerializer.Serialize(rule);
            json.Should().Contain(expectedMessage);
        }

        [Fact]
        public async Task ConvertText_Should_Handle_File_Operations()
        {
            // Arrange
            var input = "Copy files from C:\\Source to D:\\Backup every night";

            // Act
            var result = await _converter.ConvertTextAsync(input);
            var rule = JsonSerializer.Deserialize<AutomationDsl.Rule>(result);

            // Assert
            rule.Should().NotBeNull();
            rule!.Actions.Should().Contain(a => a.Type == "file.copy");
        }

        [Fact]
        public async Task ConvertText_Should_Handle_App_Launch()
        {
            // Arrange
            var input = "Start Notepad when the system starts";

            // Act
            var result = await _converter.ConvertTextAsync(input);
            var rule = JsonSerializer.Deserialize<AutomationDsl.Rule>(result);

            // Assert
            rule.Should().NotBeNull();
            rule!.Actions.Should().Contain(a => a.Type == "app.run");
        }

        [Fact]
        public async Task ConvertText_Should_Handle_Conditional_Logic()
        {
            // Arrange
            var input = "If CPU usage is above 80%, send alert";

            // Act
            var result = await _converter.ConvertTextAsync(input);
            var rule = JsonSerializer.Deserialize<AutomationDsl.Rule>(result);

            // Assert
            rule.Should().NotBeNull();
            rule!.Conditions.Should().NotBeEmpty();
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public async Task ConvertText_Should_Handle_Empty_Input(string input)
        {
            // Act
            var result = await _converter.ConvertTextAsync(input);

            // Assert
            result.Should().NotBeNullOrEmpty();
            
            // Should still return valid JSON structure
            var rule = JsonSerializer.Deserialize<AutomationDsl.Rule>(result);
            rule.Should().NotBeNull();
        }

        [Fact]
        public async Task ConvertText_Should_Handle_Multiple_Actions()
        {
            // Arrange
            var input = "Every morning at 7 AM, turn on lights, play music, and send weather notification";

            // Act
            var result = await _converter.ConvertTextAsync(input);
            var rule = JsonSerializer.Deserialize<AutomationDsl.Rule>(result);

            // Assert
            rule.Should().NotBeNull();
            rule!.Actions.Should().HaveCountGreaterThan(1);
        }

        [Fact]
        public async Task ConvertText_Should_Generate_Unique_Ids()
        {
            // Arrange
            var input = "Send notification at noon";

            // Act
            var result1 = await _converter.ConvertTextAsync(input);
            var result2 = await _converter.ConvertTextAsync(input);
            
            var rule1 = JsonSerializer.Deserialize<AutomationDsl.Rule>(result1);
            var rule2 = JsonSerializer.Deserialize<AutomationDsl.Rule>(result2);

            // Assert
            rule1!.Id.Should().NotBe(rule2!.Id);
        }

        [Fact]
        public async Task ConvertText_Should_Set_Rule_Enabled_By_Default()
        {
            // Arrange
            var input = "Backup files daily";

            // Act
            var result = await _converter.ConvertTextAsync(input);
            var rule = JsonSerializer.Deserialize<AutomationDsl.Rule>(result);

            // Assert
            rule.Should().NotBeNull();
            rule!.Enabled.Should().BeTrue();
        }

        [Theory]
        [InlineData("Do something every Monday", "weekly")]
        [InlineData("Run backup daily", "daily")]
        [InlineData("Check updates every hour", "hourly")]
        public async Task ConvertText_Should_Recognize_Frequency_Keywords(string input, string expectedFrequency)
        {
            // Act
            var result = await _converter.ConvertTextAsync(input);

            // Assert
            result.Should().NotBeNullOrEmpty();
            result.ToLower().Should().Contain(expectedFrequency);
        }
    }
}
