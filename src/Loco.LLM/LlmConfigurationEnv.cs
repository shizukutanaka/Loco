using System;
using System.Globalization;

namespace Loco.Llm
{
    public static class LlmConfigurationEnv
    {
        private static string? FirstNonEmpty(params string[] names)
        {
            foreach (var n in names)
            {
                var v = Environment.GetEnvironmentVariable(n);
                if (!string.IsNullOrWhiteSpace(v)) return v;
            }
            return null;
        }

        private static void SetIfMissing(ref string target, string value)
        {
            if (string.IsNullOrWhiteSpace(target))
            {
                target = value;
                return;
            }
            // Do not override explicit values; presets should only fill missing values
        }

        // Prime process environment variables based on LOCO_LLM__PRESET before configuration binding.
        // This reduces configuration burden by filling LOCO_LLM__PROVIDER/MODEL/APIENDPOINT when missing.
        public static void PrimeEnvironmentFromPreset()
        {
            var presetRaw = FirstNonEmpty("LOCO_LLM__PRESET", "LOCO_LLM_PRESET");
            if (string.IsNullOrWhiteSpace(presetRaw)) return;

            string? Get(string name) => Environment.GetEnvironmentVariable(name);
            void SetIfEmpty(string name, string value)
            {
                if (string.IsNullOrWhiteSpace(Get(name)))
                {
                    Environment.SetEnvironmentVariable(name, value);
                }
            }

            switch (presetRaw.Trim().ToUpperInvariant())
            {
                case "OPENAI":
                    SetIfEmpty("LOCO_LLM__PROVIDER", "openai");
                    SetIfEmpty("LOCO_LLM__MODEL", "gpt-4");
                    SetIfEmpty("LOCO_LLM__APIENDPOINT", "https://api.openai.com/v1/completions");
                    break;
                case "OLLAMA":
                    SetIfEmpty("LOCO_LLM__PROVIDER", "ollama");
                    SetIfEmpty("LOCO_LLM__MODEL", "llama3.1");
                    SetIfEmpty("LOCO_LLM__APIENDPOINT", "http://localhost:11434/api/generate");
                    break;
                case "OPENROUTER":
                    SetIfEmpty("LOCO_LLM__PROVIDER", "openrouter");
                    SetIfEmpty("LOCO_LLM__MODEL", "openrouter/auto");
                    SetIfEmpty("LOCO_LLM__APIENDPOINT", "https://openrouter.ai/api/v1/chat/completions");
                    break;
                default:
                    break;
            }
        }

        private static void ApplyPresetDefaults(LlmConfiguration options)
        {
            var presetRaw = FirstNonEmpty("LOCO_LLM__PRESET", "LOCO_LLM_PRESET");
            if (string.IsNullOrWhiteSpace(presetRaw)) return;

            var preset = presetRaw.Trim().ToUpperInvariant();
            switch (preset)
            {
                case "OPENAI":
                    // Preset primes defaults only when missing; never overrides explicit values
                    SetIfMissing(ref options.Provider, "openai");
                    SetIfMissing(ref options.Model, "gpt-4");
                    SetIfMissing(ref options.ApiEndpoint, "https://api.openai.com/v1/completions");
                    break;
                case "OLLAMA":
                    SetIfMissing(ref options.Provider, "ollama");
                    SetIfMissing(ref options.Model, "llama3.1");
                    SetIfMissing(ref options.ApiEndpoint, "http://localhost:11434/api/generate");
                    break;
                case "OPENROUTER":
                    SetIfMissing(ref options.Provider, "openrouter");
                    SetIfMissing(ref options.Model, "openrouter/auto");
                    SetIfMissing(ref options.ApiEndpoint, "https://openrouter.ai/api/v1/chat/completions");
                    break;
                default:
                    // Unknown preset: do nothing
                    break;
            }
        }

        // Applies environment variable values (LOCO_LLM__* then legacy fallbacks),
        // then applies preset defaults to fill any remaining gaps.
        // Env values override defaults; presets never override explicit values.
        public static void ApplyEnvironmentVariables(LlmConfiguration options)
        {
            // Provider
            var provider = FirstNonEmpty("LOCO_LLM__PROVIDER", "LOCO_LLM_PROVIDER");
            if (!string.IsNullOrWhiteSpace(provider)) options.Provider = provider;

            // Model
            var model = FirstNonEmpty("LOCO_LLM__MODEL", "LOCO_LLM_MODEL");
            if (!string.IsNullOrWhiteSpace(model)) options.Model = model;

            // ApiKey
            var apiKey = FirstNonEmpty("LOCO_LLM__APIKEY", "LOCO_LLM_API_KEY", "LOCO_LLM_APIKEY");
            if (!string.IsNullOrWhiteSpace(apiKey)) options.ApiKey = apiKey;

            // ApiEndpoint
            var endpoint = FirstNonEmpty("LOCO_LLM__APIENDPOINT", "LOCO_LLM_API_ENDPOINT", "LOCO_LLM_APIENDPOINT");
            if (!string.IsNullOrWhiteSpace(endpoint)) options.ApiEndpoint = endpoint;

            // MaxTokens
            var maxTokens = FirstNonEmpty("LOCO_LLM__MAXTOKENS", "LOCO_LLM_MAX_TOKENS", "LOCO_LLM_MAXTOKENS");
            if (int.TryParse(maxTokens, out var mt)) options.MaxTokens = mt;

            // Temperature
            var temperature = FirstNonEmpty("LOCO_LLM__TEMPERATURE", "LOCO_LLM_TEMPERATURE");
            if (double.TryParse(temperature, NumberStyles.Any, CultureInfo.InvariantCulture, out var temp))
                options.Temperature = temp;

            // HttpTimeoutMs
            var httpTimeout = FirstNonEmpty("LOCO_LLM__HTTPTIMEOUTMS", "LOCO_LLM_HTTPTIMEOUTMS", "LOCO_LLM_HTTP_TIMEOUT_MS");
            if (int.TryParse(httpTimeout, out var toMs)) options.HttpTimeoutMs = toMs;

            // Finally, apply preset defaults to fill any remaining unset values
            ApplyPresetDefaults(options);
        }
    }
}
