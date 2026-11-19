using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Loco.Cli.Commands;
using Xunit;

namespace Loco.Cli.Tests
{
    public class WorkflowExitCodeTests
    {
        private static async Task<int> RunAsync(string json)
        {
            var path = Path.Combine(Path.GetTempPath(), $"loco-exit-{Path.GetRandomFileName()}.json");
            await File.WriteAllTextAsync(path, json);
            try
            {
                return await new WorkflowCommand().InvokeAsync(new[] { path });
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public async Task Workflow_WhenAllStepsSucceed_ExitsZero()
        {
            var exitCode = await RunAsync("""
                { "id": "ok", "name": "OK", "steps": [
                  { "id": "s1", "name": "Say hi", "type": "log", "message": "hi" } ] }
                """);

            Assert.Equal(0, exitCode);
        }

        [Fact]
        public async Task Workflow_WithUnsupportedStepType_ExitsNonZeroAndSkipsLaterSteps()
        {
            var marker = Path.Combine(Path.GetTempPath(), $"loco-marker-{Path.GetRandomFileName()}");
            var copy = marker + ".copy";
            File.WriteAllText(marker, "x");
            try
            {
                var exitCode = await RunAsync($$"""
                    { "id": "bad", "name": "Bad", "steps": [
                      { "id": "s1", "name": "Notify", "type": "notification", "message": "done" },
                      { "id": "s2", "name": "Copy marker", "type": "file", "source": {{JsonSerializer.Serialize(marker)}}, "destination": {{JsonSerializer.Serialize(copy)}} } ] }
                    """);

                Assert.Equal(1, exitCode);
                Assert.False(File.Exists(copy));
            }
            finally
            {
                File.Delete(marker);
                File.Delete(copy);
            }
        }

        [Fact]
        public async Task Workflow_LaterStep_RunsWhenEarlierStepsSucceed()
        {
            var marker = Path.Combine(Path.GetTempPath(), $"loco-marker-{Path.GetRandomFileName()}");
            var copy = marker + ".copy";
            File.WriteAllText(marker, "x");
            try
            {
                var exitCode = await RunAsync($$"""
                    { "id": "good", "name": "Good", "steps": [
                      { "id": "s1", "name": "Say hi", "type": "log", "message": "hi" },
                      { "id": "s2", "name": "Copy marker", "type": "file", "source": {{JsonSerializer.Serialize(marker)}}, "destination": {{JsonSerializer.Serialize(copy)}} } ] }
                    """);

                Assert.Equal(0, exitCode);
                Assert.True(File.Exists(copy));
            }
            finally
            {
                File.Delete(marker);
                File.Delete(copy);
            }
        }
    }
}
