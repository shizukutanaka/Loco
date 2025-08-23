using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Loco.Core.Services
{
    /// <summary>
    /// Language detection and management service
    /// Following Rob Pike's simplicity principle
    /// </summary>
    public static class LanguageManager
    {
        // Top 100 languages by speakers, sorted by priority
        private static readonly Dictionary<string, string> SupportedLanguages = new()
        {
            // Major languages
            { "en", "English" },
            { "zh", "中文 (Chinese)" },
            { "es", "Español (Spanish)" },
            { "hi", "हिन्दी (Hindi)" },
            { "ar", "العربية (Arabic)" },
            { "bn", "বাংলা (Bengali)" },
            { "pt", "Português (Portuguese)" },
            { "ru", "Русский (Russian)" },
            { "ja", "日本語 (Japanese)" },
            { "pa", "ਪੰਜਾਬੀ (Punjabi)" },
            { "de", "Deutsch (German)" },
            { "jv", "Javanese" },
            { "ko", "한국어 (Korean)" },
            { "fr", "Français (French)" },
            { "te", "తెలుగు (Telugu)" },
            { "mr", "मराठी (Marathi)" },
            { "ta", "தமிழ் (Tamil)" },
            { "vi", "Tiếng Việt (Vietnamese)" },
            { "ur", "اردو (Urdu)" },
            { "it", "Italiano (Italian)" },
            { "tr", "Türkçe (Turkish)" },
            { "th", "ไทย (Thai)" },
            { "gu", "ગુજરાતી (Gujarati)" },
            { "fa", "فارسی (Persian)" },
            { "pl", "Polski (Polish)" },
            { "uk", "Українська (Ukrainian)" },
            { "kn", "ಕನ್ನಡ (Kannada)" },
            { "ml", "മലയാളം (Malayalam)" },
            { "or", "ଓଡ଼ିଆ (Odia)" },
            { "my", "မြန်မာ (Burmese)" },
            { "nl", "Nederlands (Dutch)" },
            { "yo", "Yorùbá" },
            { "uz", "Oʻzbek (Uzbek)" },
            { "sd", "سنڌي (Sindhi)" },
            { "am", "አማርኛ (Amharic)" },
            { "ff", "Fulfulde" },
            { "ha", "Hausa" },
            { "ps", "پښتو (Pashto)" },
            { "ro", "Română (Romanian)" },
            { "ne", "नेपाली (Nepali)" },
            { "si", "සිංහල (Sinhala)" },
            { "cs", "Čeština (Czech)" },
            { "sv", "Svenska (Swedish)" },
            { "hu", "Magyar (Hungarian)" },
            { "el", "Ελληνικά (Greek)" },
            { "fi", "Suomi (Finnish)" },
            { "no", "Norsk (Norwegian)" },
            { "da", "Dansk (Danish)" },
            { "bg", "Български (Bulgarian)" },
            { "sk", "Slovenčina (Slovak)" },
            { "he", "עברית (Hebrew)" },
            { "ms", "Bahasa Melayu (Malay)" },
            { "id", "Bahasa Indonesia" },
            { "sq", "Shqip (Albanian)" },
            { "ka", "ქართული (Georgian)" },
            { "az", "Azərbaycan (Azerbaijani)" },
            { "kk", "Қазақ (Kazakh)" },
            { "ky", "Кыргыз (Kyrgyz)" },
            { "tg", "Тоҷикӣ (Tajik)" },
            { "tk", "Türkmen" },
            { "mn", "Монгол (Mongolian)" },
            { "bo", "བོད་སྐད (Tibetan)" },
            { "km", "ខ្មែរ (Khmer)" },
            { "lo", "ລາວ (Lao)" },
            { "et", "Eesti (Estonian)" },
            { "lv", "Latviešu (Latvian)" },
            { "lt", "Lietuvių (Lithuanian)" },
            { "hr", "Hrvatski (Croatian)" },
            { "sr", "Српски (Serbian)" },
            { "bs", "Bosanski (Bosnian)" },
            { "mk", "Македонски (Macedonian)" },
            { "sl", "Slovenščina (Slovenian)" },
            { "be", "Беларуская (Belarusian)" },
            { "hy", "Հայերեն (Armenian)" },
            { "cy", "Cymraeg (Welsh)" },
            { "ga", "Gaeilge (Irish)" },
            { "gd", "Gàidhlig (Scottish Gaelic)" },
            { "is", "Íslenska (Icelandic)" },
            { "mt", "Malti (Maltese)" },
            { "lb", "Lëtzebuergesch (Luxembourgish)" },
            { "eu", "Euskara (Basque)" },
            { "ca", "Català (Catalan)" },
            { "gl", "Galego (Galician)" },
            { "af", "Afrikaans" },
            { "sw", "Kiswahili (Swahili)" },
            { "zu", "isiZulu" },
            { "xh", "isiXhosa" },
            { "ig", "Igbo" },
            { "rw", "Kinyarwanda" },
            { "so", "Soomaali (Somali)" },
            { "ti", "ትግርኛ (Tigrinya)" },
            { "mg", "Malagasy" },
            { "eo", "Esperanto" },
            { "la", "Latina (Latin)" },
            { "tl", "Tagalog" },
            { "ceb", "Cebuano" },
            { "haw", "ʻŌlelo Hawaiʻi (Hawaiian)" },
            { "mi", "Te Reo Māori" },
            { "sm", "Gagana Samoa (Samoan)" },
            { "fj", "Na Vosa Vakaviti (Fijian)" },
            { "to", "Lea faka-Tonga (Tongan)" },
            { "ht", "Kreyòl ayisyen (Haitian Creole)" },
            { "qu", "Runa Simi (Quechua)" },
            { "gn", "Avañe'ẽ (Guarani)" }
        };

        /// <summary>
        /// Get all supported language codes
        /// </summary>
        public static IEnumerable<string> GetSupportedLanguageCodes()
        {
            return SupportedLanguages.Keys;
        }

        /// <summary>
        /// Get language display name
        /// </summary>
        public static string GetLanguageName(string code)
        {
            return SupportedLanguages.TryGetValue(code.ToLower(), out var name) 
                ? name 
                : code.ToUpper();
        }

        /// <summary>
        /// Detect language from system or environment
        /// </summary>
        public static string DetectSystemLanguage()
        {
            // Check environment variable first
            var envLang = Environment.GetEnvironmentVariable("LOCO_LANG") 
                       ?? Environment.GetEnvironmentVariable("LANG");
            
            if (!string.IsNullOrEmpty(envLang))
            {
                var lang = NormalizeLanguageCode(envLang);
                if (IsSupported(lang)) return lang;
            }

            // Use system culture
            var culture = CultureInfo.CurrentUICulture;
            var langCode = culture.TwoLetterISOLanguageName.ToLower();
            
            return IsSupported(langCode) ? langCode : "en";
        }

        /// <summary>
        /// Check if language is supported
        /// </summary>
        public static bool IsSupported(string code)
        {
            return SupportedLanguages.ContainsKey(code.ToLower());
        }

        /// <summary>
        /// Normalize language code (e.g., "en-US" -> "en")
        /// </summary>
        public static string NormalizeLanguageCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return "en";
            
            code = code.Trim().ToLower();
            
            // Extract base language from codes like "en-US", "pt-BR"
            if (code.Contains('-') || code.Contains('_'))
            {
                code = code.Split(new[] { '-', '_' })[0];
            }
            
            return IsSupported(code) ? code : "en";
        }

        /// <summary>
        /// Get fallback chain for a language
        /// </summary>
        public static string[] GetFallbackChain(string code)
        {
            code = NormalizeLanguageCode(code);
            
            var chain = new List<string> { code };
            
            // Add regional fallbacks
            var fallbacks = code switch
            {
                "zh" => new[] { "zh-hans", "zh-cn" },
                "pt" => new[] { "pt-br" },
                "es" => new[] { "es-mx", "es-es" },
                _ => Array.Empty<string>()
            };
            
            chain.AddRange(fallbacks);
            
            // Always fall back to English
            if (code != "en")
            {
                chain.Add("en");
            }
            
            return chain.Distinct().ToArray();
        }
    }
}
