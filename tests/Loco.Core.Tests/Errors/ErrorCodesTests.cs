using Xunit;
using Loco.Core.Errors;

namespace Loco.Core.Tests.Errors
{
    public class ErrorCodesTests
    {
        [Fact]
        public void ErrorCodes_Should_Have_Consistent_Format()
        {
            // Verify error codes follow the pattern
            Assert.StartsWith("LOCO_", LocoErrorCodes.CONFIG_FILE_NOT_FOUND);
            Assert.StartsWith("LOCO_", LocoErrorCodes.RULE_NOT_FOUND);
            Assert.StartsWith("LOCO_", LocoErrorCodes.FLOW_NOT_FOUND);
            Assert.StartsWith("LOCO_", LocoErrorCodes.STORAGE_NOT_AVAILABLE);
            Assert.StartsWith("LOCO_", LocoErrorCodes.SECURITY_INVALID_PATH);
            Assert.StartsWith("LOCO_", LocoErrorCodes.RESOURCE_INSUFFICIENT_MEMORY);
            Assert.StartsWith("LOCO_", LocoErrorCodes.ENGINE_NOT_RUNNING);
            Assert.StartsWith("LOCO_", LocoErrorCodes.GENERAL_ERROR);
        }

        [Fact]
        public void ErrorCodes_Should_Be_Unique()
        {
            // Collect all error codes
            var codes = new[]
            {
                LocoErrorCodes.CONFIG_FILE_NOT_FOUND,
                LocoErrorCodes.CONFIG_INVALID_FORMAT,
                LocoErrorCodes.RULE_NOT_FOUND,
                LocoErrorCodes.RULE_EXECUTION_FAILED,
                LocoErrorCodes.FLOW_NOT_FOUND,
                LocoErrorCodes.STORAGE_NOT_AVAILABLE,
                LocoErrorCodes.SECURITY_INVALID_PATH,
                LocoErrorCodes.RESOURCE_INSUFFICIENT_MEMORY,
                LocoErrorCodes.ENGINE_NOT_RUNNING,
                LocoErrorCodes.GENERAL_ERROR
            };

            // Verify uniqueness
            var uniqueCodes = new HashSet<string>(codes);
            Assert.Equal(codes.Length, uniqueCodes.Count);
        }

        [Fact]
        public void ErrorMessages_Should_Be_Informative()
        {
            // Test configuration error message
            var configMsg = LocoErrorMessages.GetConfigFileNotFoundMessage("/etc/loco.conf");
            Assert.NotEmpty(configMsg);
            Assert.Contains("/etc/loco.conf", configMsg);

            // Test rule error message
            var ruleMsg = LocoErrorMessages.GetRuleNotFoundMessage("rule-123");
            Assert.NotEmpty(ruleMsg);
            Assert.Contains("rule-123", ruleMsg);

            // Test flow error message
            var flowMsg = LocoErrorMessages.GetFlowNotFoundMessage("flow-456");
            Assert.NotEmpty(flowMsg);
            Assert.Contains("flow-456", flowMsg);

            // Test security error message
            var secMsg = LocoErrorMessages.GetSecurityInvalidPathMessage("/etc/passwd");
            Assert.NotEmpty(secMsg);
            Assert.Contains("/etc/passwd", secMsg);

            // Test resource error message
            var resMsg = LocoErrorMessages.GetResourceInsufficientMemoryMessage(1024, 512);
            Assert.NotEmpty(resMsg);
            Assert.Contains("1024", resMsg);
            Assert.Contains("512", resMsg);
        }

        [Fact]
        public void ErrorCodes_Should_Cover_Main_Categories()
        {
            // Verify main error categories are covered
            var configCode = LocoErrorCodes.CONFIG_FILE_NOT_FOUND;
            var ruleCode = LocoErrorCodes.RULE_NOT_FOUND;
            var flowCode = LocoErrorCodes.FLOW_NOT_FOUND;
            var storageCode = LocoErrorCodes.STORAGE_NOT_AVAILABLE;
            var secCode = LocoErrorCodes.SECURITY_INVALID_PATH;
            var resCode = LocoErrorCodes.RESOURCE_INSUFFICIENT_MEMORY;
            var engineCode = LocoErrorCodes.ENGINE_NOT_RUNNING;

            Assert.NotNull(configCode);
            Assert.NotNull(ruleCode);
            Assert.NotNull(flowCode);
            Assert.NotNull(storageCode);
            Assert.NotNull(secCode);
            Assert.NotNull(resCode);
            Assert.NotNull(engineCode);
        }

        [Fact]
        public void RuleExecutionError_Should_Accept_Reason()
        {
            var error = LocoErrorMessages.GetRuleExecutionFailedMessage("test-rule", "Invalid action type");
            Assert.Contains("test-rule", error);
            Assert.Contains("Invalid action type", error);
        }

        [Fact]
        public void TimeoutError_Should_Include_Duration()
        {
            var error = LocoErrorMessages.GetRuleTimeoutMessage("slow-rule", 30);
            Assert.Contains("slow-rule", error);
            Assert.Contains("30", error);
        }

        [Fact]
        public void ResourceError_Should_Show_Required_And_Available()
        {
            var error = LocoErrorMessages.GetResourceInsufficientDiskMessage(1000000, 500000);
            Assert.Contains("1000000", error);
            Assert.Contains("500000", error);
        }
    }
}
