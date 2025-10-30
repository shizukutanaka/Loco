using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.AI;

/// <summary>
/// AI-powered translation service for dynamic localization
/// Supports 50+ languages with real-time translation capabilities
/// </summary>
public class AITranslationService : ITranslationService, IDisposable
{
    private readonly ILlmService _llmService;
    private readonly ILogger<AITranslationService> _logger;
    private readonly Dictionary<string, Dictionary<string, string>> _translationCache = new();
    private readonly SemaphoreSlim _cacheLock = new(1, 1);
    private bool _disposed;

    public AITranslationService(ILlmService llmService, ILogger<AITranslationService> logger)
    {
        _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Translate text using AI with caching support
    /// </summary>
    public async Task<string> TranslateAsync(string text, string targetLanguage, string sourceLanguage = "auto", CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        if (string.IsNullOrEmpty(targetLanguage))
            return text;

        var cacheKey = $"{sourceLanguage}:{targetLanguage}:{text.GetHashCode()}";

        // Check cache first
        await _cacheLock.WaitAsync(cancellationToken);
        try
        {
            if (_translationCache.TryGetValue(targetLanguage, out var languageCache) &&
                languageCache.TryGetValue(cacheKey, out var cachedTranslation))
            {
                _logger?.LogDebug("Translation cache hit for {TargetLanguage}: {Text}", targetLanguage, text);
                return cachedTranslation;
            }
        }
        finally
        {
            _cacheLock.Release();
        }

        try
        {
            _logger?.LogInformation("Translating to {TargetLanguage}: {Text}", targetLanguage, text);

            var translation = await PerformTranslationAsync(text, targetLanguage, sourceLanguage, cancellationToken);

            // Cache the result
            await _cacheLock.WaitAsync(cancellationToken);
            try
            {
                if (!_translationCache.ContainsKey(targetLanguage))
                    _translationCache[targetLanguage] = new Dictionary<string, string>();

                _translationCache[targetLanguage][cacheKey] = translation;
            }
            finally
            {
                _cacheLock.Release();
            }

            return translation;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "AI translation failed for {TargetLanguage}", targetLanguage);
            return text; // Fallback to original text
        }
    }

    /// <summary>
    /// Translate multiple texts in batch
    /// </summary>
    public async Task<Dictionary<string, string>> TranslateBatchAsync(
        Dictionary<string, string> texts,
        string targetLanguage,
        string sourceLanguage = "auto",
        CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<string, string>();

        foreach (var (key, text) in texts)
        {
            try
            {
                var translation = await TranslateAsync(text, targetLanguage, sourceLanguage, cancellationToken);
                results[key] = translation;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Batch translation failed for key {Key}", key);
                results[key] = text; // Fallback to original text
            }
        }

        return results;
    }

    /// <summary>
    /// Detect language of given text
    /// </summary>
    public async Task<string> DetectLanguageAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(text))
            return "unknown";

        try
        {
            var prompt = $"Detect the language of this text and respond with only the language code (e.g., 'en', 'ja', 'fr', etc.):\n\n{text.Substring(0, Math.Min(500, text.Length))}";

            var response = await _llmService.CompleteAsync(prompt, new LlmOptions
            {
                Temperature = 0.1f,
                MaxTokens = 10
            }, cancellationToken);

            if (response.Success && !string.IsNullOrEmpty(response.Text))
            {
                var detectedLanguage = response.Text.Trim().Split('\n')[0].Trim().ToLower();
                _logger?.LogInformation("Detected language: {DetectedLanguage} for text: {Text}", detectedLanguage, text.Substring(0, 50));
                return detectedLanguage;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Language detection failed");
        }

        return "unknown";
    }

    /// <summary>
    /// Get supported languages for translation
    /// </summary>
    public async Task<List<LanguageInfo>> GetSupportedLanguagesAsync(CancellationToken cancellationToken = default)
    {
        // Return comprehensive list of supported languages
        return new List<LanguageInfo>
        {
            new LanguageInfo { Code = "en", Name = "English", NativeName = "English", Region = "US", CalendarType = "Gregorian", BusinessRegions = new[] { "US", "UK", "AU" }, SupportedCurrencies = new[] { "USD", "GBP", "AUD" } },
            new LanguageInfo { Code = "ja", Name = "Japanese", NativeName = "日本語", Region = "JP", CalendarType = "Japanese", BusinessRegions = new[] { "JP" }, SupportedCurrencies = new[] { "JPY" } },
            new LanguageInfo { Code = "zh", Name = "Chinese", NativeName = "中文", Region = "CN", CalendarType = "Chinese", BusinessRegions = new[] { "CN", "HK", "TW" }, SupportedCurrencies = new[] { "CNY", "HKD", "TWD" } },
            new LanguageInfo { Code = "ko", Name = "Korean", NativeName = "한국어", Region = "KR", CalendarType = "Gregorian", BusinessRegions = new[] { "KR" }, SupportedCurrencies = new[] { "KRW" } },
            new LanguageInfo { Code = "es", Name = "Spanish", NativeName = "Español", Region = "ES", CalendarType = "Gregorian", BusinessRegions = new[] { "ES", "MX", "AR" }, SupportedCurrencies = new[] { "EUR", "MXN", "ARS" } },
            new LanguageInfo { Code = "de", Name = "German", NativeName = "Deutsch", Region = "DE", CalendarType = "Gregorian", BusinessRegions = new[] { "DE", "AT", "CH" }, SupportedCurrencies = new[] { "EUR" } },
            new LanguageInfo { Code = "fr", Name = "French", NativeName = "Français", Region = "FR", CalendarType = "Gregorian", BusinessRegions = new[] { "FR", "CA", "BE" }, SupportedCurrencies = new[] { "EUR", "CAD" } },
            new LanguageInfo { Code = "pt", Name = "Portuguese", NativeName = "Português", Region = "BR", CalendarType = "Gregorian", BusinessRegions = new[] { "BR", "PT" }, SupportedCurrencies = new[] { "BRL", "EUR" } },
            new LanguageInfo { Code = "it", Name = "Italian", NativeName = "Italiano", Region = "IT", CalendarType = "Gregorian", BusinessRegions = new[] { "IT", "CH" }, SupportedCurrencies = new[] { "EUR" } },
            new LanguageInfo { Code = "ru", Name = "Russian", NativeName = "Русский", Region = "RU", CalendarType = "Gregorian", BusinessRegions = new[] { "RU" }, SupportedCurrencies = new[] { "RUB" } },
            new LanguageInfo { Code = "hi", Name = "Hindi", NativeName = "हिन्दी", Region = "IN", CalendarType = "Gregorian", BusinessRegions = new[] { "IN" }, SupportedCurrencies = new[] { "INR" } },
            new LanguageInfo { Code = "ar", Name = "Arabic", NativeName = "العربية", Region = "SA", IsRTL = true, CalendarType = "Hijri", BusinessRegions = new[] { "SA", "EG", "AE" }, SupportedCurrencies = new[] { "SAR", "EGP", "AED" } },
            new LanguageInfo { Code = "id", Name = "Indonesian", NativeName = "Bahasa Indonesia", Region = "ID", CalendarType = "Gregorian", BusinessRegions = new[] { "ID" }, SupportedCurrencies = new[] { "IDR" } },
            new LanguageInfo { Code = "th", Name = "Thai", NativeName = "ไทย", Region = "TH", CalendarType = "Buddhist", BusinessRegions = new[] { "TH" }, SupportedCurrencies = new[] { "THB" } },
            new LanguageInfo { Code = "nl", Name = "Dutch", NativeName = "Nederlands", Region = "NL", CalendarType = "Gregorian", BusinessRegions = new[] { "NL", "BE" }, SupportedCurrencies = new[] { "EUR" } },
            new LanguageInfo { Code = "sv", Name = "Swedish", NativeName = "Svenska", Region = "SE", CalendarType = "Gregorian", BusinessRegions = new[] { "SE" }, SupportedCurrencies = new[] { "SEK" } },
            new LanguageInfo { Code = "pl", Name = "Polish", NativeName = "Polski", Region = "PL", CalendarType = "Gregorian", BusinessRegions = new[] { "PL" }, SupportedCurrencies = new[] { "PLN" } },
            new LanguageInfo { Code = "tr", Name = "Turkish", NativeName = "Türkçe", Region = "TR", CalendarType = "Gregorian", BusinessRegions = new[] { "TR" }, SupportedCurrencies = new[] { "TRY" } },
            new LanguageInfo { Code = "vi", Name = "Vietnamese", NativeName = "Tiếng Việt", Region = "VN", CalendarType = "Gregorian", BusinessRegions = new[] { "VN" }, SupportedCurrencies = new[] { "VND" } },
            new LanguageInfo { Code = "he", Name = "Hebrew", NativeName = "עברית", Region = "IL", IsRTL = true, CalendarType = "Hebrew", BusinessRegions = new[] { "IL" }, SupportedCurrencies = new[] { "ILS" } },
            new LanguageInfo { Code = "fa", Name = "Persian", NativeName = "فارسی", Region = "IR", IsRTL = true, CalendarType = "Solar Hijri", BusinessRegions = new[] { "IR" }, SupportedCurrencies = new[] { "IRR" } },
            new LanguageInfo { Code = "cs", Name = "Czech", NativeName = "Čeština", Region = "CZ", CalendarType = "Gregorian", BusinessRegions = new[] { "CZ" }, SupportedCurrencies = new[] { "CZK" } },
            new LanguageInfo { Code = "sk", Name = "Slovak", NativeName = "Slovenčina", Region = "SK", CalendarType = "Gregorian", BusinessRegions = new[] { "SK" }, SupportedCurrencies = new[] { "EUR" } },
            new LanguageInfo { Code = "hu", Name = "Hungarian", NativeName = "Magyar", Region = "HU", CalendarType = "Gregorian", BusinessRegions = new[] { "HU" }, SupportedCurrencies = new[] { "HUF" } },
            new LanguageInfo { Code = "ro", Name = "Romanian", NativeName = "Română", Region = "RO", CalendarType = "Gregorian", BusinessRegions = new[] { "RO" }, SupportedCurrencies = new[] { "RON" } },
            new LanguageInfo { Code = "bg", Name = "Bulgarian", NativeName = "Български", Region = "BG", CalendarType = "Gregorian", BusinessRegions = new[] { "BG" }, SupportedCurrencies = new[] { "BGN" } },
            new LanguageInfo { Code = "hr", Name = "Croatian", NativeName = "Hrvatski", Region = "HR", CalendarType = "Gregorian", BusinessRegions = new[] { "HR" }, SupportedCurrencies = new[] { "HRK" } },
            new LanguageInfo { Code = "sl", Name = "Slovenian", NativeName = "Slovenščina", Region = "SI", CalendarType = "Gregorian", BusinessRegions = new[] { "SI" }, SupportedCurrencies = new[] { "EUR" } },
            new LanguageInfo { Code = "et", Name = "Estonian", NativeName = "Eesti", Region = "EE", CalendarType = "Gregorian", BusinessRegions = new[] { "EE" }, SupportedCurrencies = new[] { "EUR" } },
            new LanguageInfo { Code = "lv", Name = "Latvian", NativeName = "Latviešu", Region = "LV", CalendarType = "Gregorian", BusinessRegions = new[] { "LV" }, SupportedCurrencies = new[] { "EUR" } },
            new LanguageInfo { Code = "lt", Name = "Lithuanian", NativeName = "Lietuvių", Region = "LT", CalendarType = "Gregorian", BusinessRegions = new[] { "LT" }, SupportedCurrencies = new[] { "EUR" } },
            new LanguageInfo { Code = "mt", Name = "Maltese", NativeName = "Malti", Region = "MT", CalendarType = "Gregorian", BusinessRegions = new[] { "MT" }, SupportedCurrencies = new[] { "EUR" } },
            new LanguageInfo { Code = "ga", Name = "Irish", NativeName = "Gaeilge", Region = "IE", CalendarType = "Gregorian", BusinessRegions = new[] { "IE" }, SupportedCurrencies = new[] { "EUR" } },
            new LanguageInfo { Code = "cy", Name = "Welsh", NativeName = "Cymraeg", Region = "GB", CalendarType = "Gregorian", BusinessRegions = new[] { "GB" }, SupportedCurrencies = new[] { "GBP" } },
            new LanguageInfo { Code = "is", Name = "Icelandic", NativeName = "Íslenska", Region = "IS", CalendarType = "Gregorian", BusinessRegions = new[] { "IS" }, SupportedCurrencies = new[] { "ISK" } },
            new LanguageInfo { Code = "fo", Name = "Faroese", NativeName = "Føroyskt", Region = "FO", CalendarType = "Gregorian", BusinessRegions = new[] { "FO" }, SupportedCurrencies = new[] { "DKK" } },
            new LanguageInfo { Code = "mk", Name = "Macedonian", NativeName = "Македонски", Region = "MK", CalendarType = "Gregorian", BusinessRegions = new[] { "MK" }, SupportedCurrencies = new[] { "MKD" } },
            new LanguageInfo { Code = "sq", Name = "Albanian", NativeName = "Shqip", Region = "AL", CalendarType = "Gregorian", BusinessRegions = new[] { "AL" }, SupportedCurrencies = new[] { "ALL" } },
            new LanguageInfo { Code = "bs", Name = "Bosnian", NativeName = "Bosanski", Region = "BA", CalendarType = "Gregorian", BusinessRegions = new[] { "BA" }, SupportedCurrencies = new[] { "BAM" } },
            new LanguageInfo { Code = "sr", Name = "Serbian", NativeName = "Српски", Region = "RS", CalendarType = "Gregorian", BusinessRegions = new[] { "RS" }, SupportedCurrencies = new[] { "RSD" } },
            new LanguageInfo { Code = "me", Name = "Montenegrin", NativeName = "Crnogorski", Region = "ME", CalendarType = "Gregorian", BusinessRegions = new[] { "ME" }, SupportedCurrencies = new[] { "EUR" } },
            new LanguageInfo { Code = "ka", Name = "Georgian", NativeName = "ქართული", Region = "GE", CalendarType = "Gregorian", BusinessRegions = new[] { "GE" }, SupportedCurrencies = new[] { "GEL" } },
            new LanguageInfo { Code = "hy", Name = "Armenian", NativeName = "Հայերեն", Region = "AM", CalendarType = "Gregorian", BusinessRegions = new[] { "AM" }, SupportedCurrencies = new[] { "AMD" } },
            new LanguageInfo { Code = "az", Name = "Azerbaijani", NativeName = "Azərbaycan", Region = "AZ", CalendarType = "Gregorian", BusinessRegions = new[] { "AZ" }, SupportedCurrencies = new[] { "AZN" } },
            new LanguageInfo { Code = "kk", Name = "Kazakh", NativeName = "Қазақша", Region = "KZ", CalendarType = "Gregorian", BusinessRegions = new[] { "KZ" }, SupportedCurrencies = new[] { "KZT" } },
            new LanguageInfo { Code = "ky", Name = "Kyrgyz", NativeName = "Кыргызча", Region = "KG", CalendarType = "Gregorian", BusinessRegions = new[] { "KG" }, SupportedCurrencies = new[] { "KGS" } },
            new LanguageInfo { Code = "tg", Name = "Tajik", NativeName = "Тоҷикӣ", Region = "TJ", CalendarType = "Gregorian", BusinessRegions = new[] { "TJ" }, SupportedCurrencies = new[] { "TJS" } },
            new LanguageInfo { Code = "tk", Name = "Turkmen", NativeName = "Türkmençe", Region = "TM", CalendarType = "Gregorian", BusinessRegions = new[] { "TM" }, SupportedCurrencies = new[] { "TMT" } },
            new LanguageInfo { Code = "uz", Name = "Uzbek", NativeName = "O'zbekcha", Region = "UZ", CalendarType = "Gregorian", BusinessRegions = new[] { "UZ" }, SupportedCurrencies = new[] { "UZS" } },
            new LanguageInfo { Code = "mn", Name = "Mongolian", NativeName = "Монгол", Region = "MN", CalendarType = "Gregorian", BusinessRegions = new[] { "MN" }, SupportedCurrencies = new[] { "MNT" } },
            new LanguageInfo { Code = "ug", Name = "Uyghur", NativeName = "ئۇيغۇرچە", Region = "CN", IsRTL = true, CalendarType = "Gregorian", BusinessRegions = new[] { "CN" }, SupportedCurrencies = new[] { "CNY" } }
        };
    }

    /// <summary>
    /// Clear translation cache
    /// </summary>
    public async Task ClearCacheAsync(CancellationToken cancellationToken = default)
    {
        await _cacheLock.WaitAsync(cancellationToken);
        try
        {
            _translationCache.Clear();
            _logger?.LogInformation("Translation cache cleared");
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    /// <summary>
    /// Translate text using Agentic AI with cultural adaptation
    /// </summary>
    public async Task<string> TranslateWithCulturalAdaptationAsync(string text, string targetLanguage, string sourceLanguage = "auto", CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        var languageInfo = await GetLanguageInfoAsync(targetLanguage, cancellationToken);
        if (languageInfo == null)
            return await TranslateAsync(text, targetLanguage, sourceLanguage, cancellationToken);

        // Agentic AI: Analyze context and cultural nuances
        var contextPrompt = $"Analyze the cultural context and business practices for {languageInfo.NativeName} ({languageInfo.Region}). " +
                           $"Consider regional calendars ({languageInfo.CalendarType}), business etiquette, and local idioms. " +
                           $"Translate the following text with full cultural adaptation:\n\n{text}";

        var response = await _llmService.CompleteAsync(contextPrompt, new LlmOptions
        {
            Temperature = 0.3f,
            MaxTokens = Math.Max(200, text.Length * 3) // More tokens for cultural adaptation
        }, cancellationToken);

        if (response.Success && !string.IsNullOrEmpty(response.Text))
        {
            var adaptedTranslation = response.Text.Trim();
            _logger?.LogInformation("Agentic AI translation with cultural adaptation completed: {SourceLanguage} -> {TargetLanguage}", sourceLanguage, targetLanguage);
            return adaptedTranslation;
        }

        // Fallback to standard translation
        return await TranslateAsync(text, targetLanguage, sourceLanguage, cancellationToken);
    }

    /// <summary>
    /// Get detailed language information including cultural context
    /// </summary>
    public async Task<LanguageInfo?> GetLanguageInfoAsync(string languageCode, CancellationToken cancellationToken = default)
    {
        var languages = await GetSupportedLanguagesAsync(cancellationToken);
        return languages.FirstOrDefault(l => l.Code.Equals(languageCode, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Evaluate translation quality using multiple metrics
    /// </summary>
    public async Task<TranslationQualityMetrics> EvaluateTranslationQualityAsync(string originalText, string translatedText, string targetLanguage, CancellationToken cancellationToken = default)
    {
        var metrics = new TranslationQualityMetrics();

        // BLEU Score (simplified approximation)
        metrics.BleuScore = CalculateBleuScore(originalText, translatedText);

        // COMET Score (using LLM for evaluation)
        var evaluationPrompt = $"Evaluate the translation quality from English to {GetLanguageName(targetLanguage)}. " +
                              $"Rate accuracy (1-5), fluency (1-5), and cultural adaptation (1-5). " +
                              $"Original: {originalText}\nTranslated: {translatedText}\n\n" +
                              $"Provide scores only in JSON format: {{\"accuracy\": 0, \"fluency\": 0, \"culturalAdaptation\": 0}}";

        var response = await _llmService.CompleteAsync(evaluationPrompt, new LlmOptions
        {
            Temperature = 0.1f,
            MaxTokens = 50
        }, cancellationToken);

        if (response.Success && !string.IsNullOrEmpty(response.Text))
        {
            try
            {
                var scores = JsonSerializer.Deserialize<Dictionary<string, double>>(response.Text);
                metrics.Accuracy = scores?.GetValueOrDefault("accuracy", 0) ?? 0;
                metrics.Fluency = scores?.GetValueOrDefault("fluency", 0) ?? 0;
                metrics.CulturalAdaptation = scores?.GetValueOrDefault("culturalAdaptation", 0) ?? 0;
            }
            catch
            {
                // Fallback if JSON parsing fails
                metrics.Accuracy = 3.0; // Default moderate score
                metrics.Fluency = 3.0;
                metrics.CulturalAdaptation = 3.0;
            }
        }

        metrics.OverallScore = (metrics.Accuracy + metrics.Fluency + metrics.CulturalAdaptation) / 3.0;
        metrics.IsAcceptable = metrics.OverallScore >= 3.5;

        _logger?.LogInformation("Translation quality evaluated: Overall={OverallScore:F1}, Acceptable={IsAcceptable}", metrics.OverallScore, metrics.IsAcceptable);

        return metrics;
    }

    private double CalculateBleuScore(string original, string translated)
    {
        // Simplified BLEU score calculation
        var originalWords = original.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var translatedWords = translated.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (originalWords.Length == 0) return 0;

        var matches = originalWords.Count(w => translatedWords.Contains(w));
        return (double)matches / originalWords.Length;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _cacheLock?.Dispose();
            _translationCache.Clear();
            _disposed = true;
        }
    }
}

/// <summary>
/// Translation service interface
/// </summary>
public interface ITranslationService
{
    Task<string> TranslateWithCulturalAdaptationAsync(string text, string targetLanguage, string sourceLanguage = "auto", CancellationToken cancellationToken = default);
    Task<LanguageInfo?> GetLanguageInfoAsync(string languageCode, CancellationToken cancellationToken = default);
    Task<TranslationQualityMetrics> EvaluateTranslationQualityAsync(string originalText, string translatedText, string targetLanguage, CancellationToken cancellationToken = default);
    Task<CulturalCommunicationGuide> GenerateCommunicationGuideAsync(string primaryCulture, string targetCulture, CommunicationContext context, CancellationToken cancellationToken = default);
    Task<CulturalSensitivityScore> EvaluateCulturalSensitivityAsync(string message, string targetCulture, CommunicationContext context, CancellationToken cancellationToken = default);
    Task<AccessibilityTranslation> TranslateForAccessibilityAsync(string text, string targetLanguage, AccessibilityRequirements requirements, CancellationToken cancellationToken = default);
    Task<AccessibilityScore> EvaluateLanguageAccessibilityAsync(string text, string language, CancellationToken cancellationToken = default);
}

/// <summary>
/// コミュニケーションコンテキスト
/// </summary>
public enum CommunicationContext
{
    Business,
    Technical,
    Sales,
    Support,
    Negotiation,
    TeamCollaboration,
    Training
}

/// <summary>
/// 文化的適合性スコア
/// </summary>
public class CulturalSensitivityScore
{
    public double OverallScore { get; set; }
    public double CulturalAppropriateness { get; set; }
    public double CommunicationMatch { get; set; }
    public double EtiquetteCompliance { get; set; }
    public double MisunderstandingRisk { get; set; }
    public bool IsAppropriate { get; set; }
    public List<string> Recommendations { get; set; } = new();
}

/// <summary>
/// 文化的コミュニケーションガイド
/// </summary>
public class CulturalCommunicationGuide
{
    public LanguageInfo PrimaryCulture { get; set; } = new();
    public LanguageInfo TargetCulture { get; set; } = new();
    public CommunicationContext Context { get; set; }
    public List<CommunicationDifference> CommunicationDifferences { get; set; } = new();
    public List<string> BestPractices { get; set; } = new();
    public List<string> PotentialIssues { get; set; } = new();
    public List<string> RecommendedStrategies { get; set; } = new();
    public DateTime GeneratedAt { get; set; }
}

/// <summary>
/// コミュニケーションの違い
/// </summary>
public class CommunicationDifference
{
    public string Aspect { get; set; } = string.Empty;
    public string PrimaryCulture { get; set; } = string.Empty;
    public string TargetCulture { get; set; } = string.Empty;
    public string Impact { get; set; } = string.Empty;
}

/// <summary>
/// アクセシビリティ要件
/// </summary>
public class AccessibilityRequirements
{
    public bool UseSimpleLanguage { get; set; }
    public bool IncludeAlternativeText { get; set; }
    public bool SupportScreenReader { get; set; }
    public bool IncludeCulturalContext { get; set; }
    public string[] SupportedRegions { get; set; } = Array.Empty<string>();
}

/// <summary>
/// アクセシビリティ対応翻訳
/// </summary>
public class AccessibilityTranslation
{
    public string OriginalText { get; set; } = string.Empty;
    public string BaseTranslation { get; set; } = string.Empty;
    public string? SimplifiedTranslation { get; set; }
    public List<string> AlternativeDescriptions { get; set; } = new();
    public string? ScreenReaderOptimized { get; set; }
    public string? CulturalContext { get; set; }
    public string TargetLanguage { get; set; } = string.Empty;
    public string FinalTranslation => GetFinalTranslation();
    public AccessibilityRequirements Requirements { get; set; } = new();
    public bool IsCompliant { get; set; }
    public string ValidationReport { get; set; } = string.Empty;

    private string GetFinalTranslation()
    {
        if (!string.IsNullOrEmpty(SimplifiedTranslation)) return SimplifiedTranslation;
        if (!string.IsNullOrEmpty(ScreenReaderOptimized)) return ScreenReaderOptimized;
        return BaseTranslation;
    }
}

/// <summary>
/// アクセシビリティスコア
/// </summary>
public class AccessibilityScore
{
    public double OverallScore { get; set; }
    public double Readability { get; set; }
    public double CulturalAppropriateness { get; set; }
    public double Inclusivity { get; set; }
    public double TechnicalAccessibility { get; set; }
    public double Compliance { get; set; }
    public bool IsAccessible { get; set; }
    public List<string> Recommendations { get; set; } = new();
}

/// <summary>
/// 翻訳品質メトリクス
/// </summary>
public class TranslationQualityMetrics
{
    public double BleuScore { get; set; }
    public double Accuracy { get; set; }
    public double Fluency { get; set; }
    public double CulturalAdaptation { get; set; }
    public double OverallScore { get; set; }
    public bool IsAcceptable { get; set; }
    public Dictionary<string, object> AdditionalMetrics { get; set; } = new();
}
