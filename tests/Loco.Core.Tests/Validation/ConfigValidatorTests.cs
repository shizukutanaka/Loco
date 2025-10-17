using System;
using System.Collections.Generic;
using Xunit;
using Loco.Core.Configuration;
using Loco.Core.Validation;

namespace Loco.Core.Tests.Validation
{
    public class ConfigValidatorTests
    {
        [Fact]
        public void Validate_ValidConfig_ReturnsValidResult()
        {
            // Arrange
            var config = new LocoConfig
            {
                MaxConcurrentFlows = 5,
                DefaultTimeoutSeconds = 30,
                DefaultRetryCount = 3
            };

            // Act
            var result = ConfigValidator.Validate(config);

            // Assert
            Assert.True(result.IsValid);
            Assert.Empty(result.Errors);
            Assert.Empty(result.Warnings);
        }

        [Fact]
        public void Validate_InvalidMaxConcurrentFlows_ReturnsError()
        {
            // Arrange
            var config = new LocoConfig
            {
                MaxConcurrentFlows = 0, // Invalid
                DefaultTimeoutSeconds = 30,
                DefaultRetryCount = 3
            };

            // Act
            var result = ConfigValidator.Validate(config);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Contains("MaxConcurrentFlows"));
        }

        [Fact]
        public void Validate_HighValues_ReturnsWarnings()
        {
            // Arrange
            var config = new LocoConfig
            {
                MaxConcurrentFlows = 150, // High value
                DefaultTimeoutSeconds = 30,
                DefaultRetryCount = 3
            };

            // Act
            var result = ConfigValidator.Validate(config);

            // Assert
            Assert.True(result.IsValid); // Valid but with warnings
            Assert.Contains(result.Warnings, w => w.Contains("MaxConcurrentFlows"));
        }

        [Fact]
        public void Validate_NegativeRetryCount_ReturnsError()
        {
            // Arrange
            var config = new LocoConfig
            {
                MaxConcurrentFlows = 5,
                DefaultTimeoutSeconds = 30,
                DefaultRetryCount = -1 // Invalid
            };

            // Act
            var result = ConfigValidator.Validate(config);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Contains("DefaultRetryCount"));
        }
    }
}
