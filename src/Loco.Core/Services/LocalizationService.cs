using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Loco.Core.Interfaces;

namespace Loco.Core.Services
{
    /// <summary>
    /// JSON-based runtime localization service.
    /// Looks for JSON files under "locales" (case-insensitive) in AppContext.BaseDirectory.
    /// File names should be like "en.json", "ja.json", "pt-BR.json".
    /// </summary>
    public sealed class LocalizationService : ILocalizationService
    {
        private readonly ConcurrentDictionary<string, Dictionary<string, string>> _cache = new();
        private readonly string _localesPath;
        private string _currentLanguage = "en";
        private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
        {
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            PropertyNameCaseInsensitive = true
        };

        public LocalizationService(string? localesBasePath = null)
        {
            var baseDir = AppContext.BaseDirectory;
            // Prefer "locales"; fallback to "Locales"
            _localesPath = localesBasePath ??
                           new[] { "locales", "Locales" }
                               .Select(p => Path.Combine(baseDir, p))
                               .FirstOrDefault(Directory.Exists)
                           ?? Path.Combine(baseDir, "locales");
            if (!Directory.Exists(_localesPath))
            {
                Directory.CreateDirectory(_localesPath);
            }
        }

        public string CurrentLanguage => _currentLanguage;
        public string DefaultLanguage => "en";
        public CultureInfo CurrentCulture => GetCulture(_currentLanguage);

        public async Task SetLanguageAsync(string language)
        {
            if (string.IsNullOrWhiteSpace(language)) language = DefaultLanguage;

            // Normalize e.g., zh-CN, pt-BR
            var normalized = NormalizeLang(language);
            _currentLanguage = normalized;

            // prime cache
            await EnsureLoadedAsync(normalized).ConfigureAwait(false);

            // Also try base language (e.g., "pt" from "pt-BR") for fallback chain
            var baseLang = BaseLanguage(normalized);
            if (!string.Equals(baseLang, normalized, StringComparison.OrdinalIgnoreCase))
            {
                await EnsureLoadedAsync(baseLang).ConfigureAwait(false);
            }

            // Always ensure default language
            await EnsureLoadedAsync(DefaultLanguage).ConfigureAwait(false);

            // Set process/UI culture
            var ci = GetCulture(normalized);
            CultureInfo.DefaultThreadCurrentCulture = ci;
            CultureInfo.DefaultThreadCurrentUICulture = ci;
        }

        public string T(string key, params object[]? formatArgs)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;

            // Current -> base -> default -> key
            if (TryGetValue(_currentLanguage, key, out var v) ||
                TryGetValue(BaseLanguage(_currentLanguage), key, out v) ||
                TryGetValue(DefaultLanguage, key, out v))
            {
                try
                {
                    return (formatArgs is { Length: > 0 })
                        ? string.Format(CurrentCulture, v, formatArgs)
                        : v;
                }
                catch
                {
                    // If formatting fails, fall back to raw value
                    return v;
                }
            }

            return key; // last resort
        }

        public string[] GetAvailableLanguages()
        {
            if (!Directory.Exists(_localesPath)) return Array.Empty<string>();
            return Directory.EnumerateFiles(_localesPath, "*.json", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileNameWithoutExtension)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private async Task EnsureLoadedAsync(string lang)
        {
            lang = NormalizeLang(lang);
            if (_cache.ContainsKey(lang)) return;

            var file = Path.Combine(_localesPath, lang + ".json");
            if (!File.Exists(file))
            {
                // Try case-insensitive match
                var match = Directory.Exists(_localesPath)
                    ? Directory.EnumerateFiles(_localesPath, "*.json", SearchOption.TopDirectoryOnly)
                        .FirstOrDefault(f => string.Equals(Path.GetFileNameWithoutExtension(f), lang, StringComparison.OrdinalIgnoreCase))
                    : null;
                if (match != null) file = match;
            }

            Dictionary<string, string> map = new(StringComparer.OrdinalIgnoreCase);
            if (File.Exists(file))
            {
                await using var fs = File.OpenRead(file);
                var doc = await JsonSerializer.DeserializeAsync<Dictionary<string, string>>(fs, _jsonOptions).ConfigureAwait(false);
                if (doc != null)
                {
                    foreach (var kv in doc)
                    {
                        // skip empty keys
                        if (!string.IsNullOrWhiteSpace(kv.Key))
                        {
                            map[kv.Key] = kv.Value ?? string.Empty;
                        }
                    }
                }
            }
            _cache[lang] = map;
        }

        private bool TryGetValue(string lang, string key, out string value)
        {
            lang = NormalizeLang(lang);
            if (_cache.TryGetValue(lang, out var dict) && dict.TryGetValue(key, out value!))
            {
                return true;
            }
            value = string.Empty;
            return false;
        }

        private static string NormalizeLang(string lang)
        {
            return lang.Replace('_', '-').Trim();
        }

        private static string BaseLanguage(string lang)
        {
            var idx = lang.IndexOf('-');
            return idx > 0 ? lang[..idx] : lang;
        }

        private static CultureInfo GetCulture(string lang)
        {
            try
            {
                return CultureInfo.GetCultureInfo(lang);
            }
            catch
            {
                return CultureInfo.InvariantCulture;
            }
        }
    }
}
