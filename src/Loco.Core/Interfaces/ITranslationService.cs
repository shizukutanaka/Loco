using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Loco.Core.Interfaces;

/// <summary>
/// Interface for AI-powered translation services
/// </summary>
public interface ITranslationService
{
    /// <summary>
    /// Translate text to target language
    /// </summary>
    Task<string> TranslateAsync(string text, string targetLanguage, string sourceLanguage = "auto", CancellationToken cancellationToken = default);

    /// <summary>
    /// Translate multiple texts in batch
    /// </summary>
    Task<Dictionary<string, string>> TranslateBatchAsync(Dictionary<string, string> texts, string targetLanguage, string sourceLanguage = "auto", CancellationToken cancellationToken = default);

    /// <summary>
    /// Detect language of text
    /// </summary>
    Task<string> DetectLanguageAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get list of supported languages
    /// </summary>
    Task<List<LanguageInfo>> GetSupportedLanguagesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Clear translation cache
    /// </summary>
    Task ClearCacheAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Language information model
/// </summary>
public class LanguageInfo
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NativeName { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public bool IsRTL { get; set; }
    public bool IsBeta { get; set; }
}
