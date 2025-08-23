using System;
using Loco.Llm;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Loco.Cli.Tests
{
    public class LlmPresetBindingTests
    {
        [Fact]
        public void Preset_Ollama_Fills_When_Missing()
        {
            var oldPreset = Environment.GetEnvironmentVariable("LOCO_LLM__PRESET");
            var oldProvider = Environment.GetEnvironmentVariable("LOCO_LLM__PROVIDER");
            var oldModel = Environment.GetEnvironmentVariable("LOCO_LLM__MODEL");
            var oldEndpoint = Environment.GetEnvironmentVariable("LOCO_LLM__APIENDPOINT");
            try
            {
                Environment.SetEnvironmentVariable("LOCO_LLM__PRESET", "OLLAMA");
                Environment.SetEnvironmentVariable("LOCO_LLM__PROVIDER", null);
                Environment.SetEnvironmentVariable("LOCO_LLM__MODEL", null);
                Environment.SetEnvironmentVariable("LOCO_LLM__APIENDPOINT", null);

                var opts = new LlmConfiguration();
                // simulate host PostConfigure path
                LlmConfigurationEnv.ApplyEnvironmentVariables(opts);

                Assert.Equal("ollama", opts.Provider);
                Assert.Equal("llama3.1", opts.Model);
                Assert.Equal("http://localhost:11434/api/generate", opts.ApiEndpoint);
            }
            finally
            {
                Environment.SetEnvironmentVariable("LOCO_LLM__PRESET", oldPreset);
                Environment.SetEnvironmentVariable("LOCO_LLM__PROVIDER", oldProvider);
                Environment.SetEnvironmentVariable("LOCO_LLM__MODEL", oldModel);
                Environment.SetEnvironmentVariable("LOCO_LLM__APIENDPOINT", oldEndpoint);
            }
        }

        [Fact]
        public void Preset_Does_Not_Override_Explicit_Values()
        {
            var oldPreset = Environment.GetEnvironmentVariable("LOCO_LLM__PRESET");
            var oldModel = Environment.GetEnvironmentVariable("LOCO_LLM__MODEL");
            try
            {
                Environment.SetEnvironmentVariable("LOCO_LLM__PRESET", "OPENAI");
                Environment.SetEnvironmentVariable("LOCO_LLM__MODEL", "custom-model");

                var opts = new LlmConfiguration();
                LlmConfigurationEnv.ApplyEnvironmentVariables(opts);

                Assert.Equal("custom-model", opts.Model);
            }
            finally
            {
                Environment.SetEnvironmentVariable("LOCO_LLM__PRESET", oldPreset);
                Environment.SetEnvironmentVariable("LOCO_LLM__MODEL", oldModel);
            }
        }
    }
}
