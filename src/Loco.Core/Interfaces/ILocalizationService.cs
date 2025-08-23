using System.Globalization;
using System.Threading.Tasks;

namespace Loco.Core.Interfaces
{
    /// <summary>
    /// JSON-based runtime localization service interface.
    /// </summary>
    public interface ILocalizationService
    {
        /// <summary>Current language/culture code (e.g., "en", "ja", "fr").</summary>
        string CurrentLanguage { get; }

        /// <summary>Set the current language and load its resources.</summary>
        Task SetLanguageAsync(string language);

        /// <summary>Translate a key to the current language. Falls back to default language and key if missing.</summary>
        string T(string key, params object[]? formatArgs);

        /// <summary>Return available languages discovered in the locales directory.</summary>
        string[] GetAvailableLanguages();

        /// <summary>CultureInfo representing the current UI culture.</summary>
        CultureInfo CurrentCulture { get; }

        /// <summary>Default language used when a key or language is missing (defaults to "en").</summary>
        string DefaultLanguage { get; }
    }
}
