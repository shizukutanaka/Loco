using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Resources;
using System.Threading;

namespace Loco.Cli;

/// <summary>
/// Localization manager for the Loco CLI
/// </summary>
public class LocalizationManager
{
    private readonly Dictionary<string, Dictionary<string, string>> _resources = new();
    private CultureInfo _currentCulture = CultureInfo.CurrentCulture;

    public CultureInfo CurrentCulture
    {
        get => _currentCulture;
        set
        {
            _currentCulture = value;
            Thread.CurrentThread.CurrentCulture = value;
            Thread.CurrentThread.CurrentUICulture = value;
        }
    }

    public LocalizationManager()
    {
        InitializeResources();
    }

    private void InitializeResources()
    {
        // English resources
        _resources["en"] = new Dictionary<string, string>
        {
            ["Version"] = "Version",
            ["HealthCheck"] = "Health Check",
            ["UpdateCheck"] = "Update Check",
            ["EngineStarted"] = "Engine started successfully",
            ["EngineStopped"] = "Engine stopped",
            ["InvalidCommand"] = "Invalid command",
            ["CommandCompleted"] = "Command completed successfully",
            ["ErrorOccurred"] = "An error occurred",
            ["FileNotFound"] = "File not found",
            ["InvalidPath"] = "Invalid path",
            ["AccessDenied"] = "Access denied",
            ["ConfigurationError"] = "Configuration error",
            ["NetworkError"] = "Network error",
            ["TimeoutError"] = "Operation timed out",
            ["ValidationError"] = "Validation error",
            ["Success"] = "Success",
            ["Failed"] = "Failed",
            ["Warning"] = "Warning",
            ["Info"] = "Information"
        };

        // Japanese resources
        _resources["ja"] = new Dictionary<string, string>
        {
            ["Version"] = "バージョン",
            ["HealthCheck"] = "ヘルスチェック",
            ["UpdateCheck"] = "アップデート確認",
            ["EngineStarted"] = "エンジンが正常に起動しました",
            ["EngineStopped"] = "エンジンが停止しました",
            ["InvalidCommand"] = "無効なコマンド",
            ["CommandCompleted"] = "コマンドが正常に完了しました",
            ["ErrorOccurred"] = "エラーが発生しました",
            ["FileNotFound"] = "ファイルが見つかりません",
            ["InvalidPath"] = "無効なパス",
            ["AccessDenied"] = "アクセスが拒否されました",
            ["ConfigurationError"] = "設定エラー",
            ["NetworkError"] = "ネットワークエラー",
            ["TimeoutError"] = "操作がタイムアウトしました",
            ["ValidationError"] = "検証エラー",
            ["Success"] = "成功",
            ["Failed"] = "失敗",
            ["Warning"] = "警告",
            ["Info"] = "情報"
        };
    }

    /// <summary>
    /// Detect the best culture based on system settings
    /// </summary>
    public CultureInfo DetectBestCulture()
    {
        // Check environment variables first
        var envCulture = Environment.GetEnvironmentVariable("LOCO_CULTURE") ??
                        Environment.GetEnvironmentVariable("LANG") ??
                        Environment.GetEnvironmentVariable("LC_ALL");

        if (!string.IsNullOrEmpty(envCulture))
        {
            try
            {
                // Extract language code from environment variable
                var langCode = envCulture.Split('.')[0].Split('_')[0];
                if (_resources.ContainsKey(langCode))
                {
                    return new CultureInfo(langCode);
                }
            }
            catch
            {
                // Ignore parsing errors
            }
        }

        // Check current thread culture
        var currentLang = Thread.CurrentThread.CurrentCulture.TwoLetterISOLanguageName;
        if (_resources.ContainsKey(currentLang))
        {
            return Thread.CurrentThread.CurrentCulture;
        }

        // Check system culture
        var systemLang = CultureInfo.InstalledUICulture.TwoLetterISOLanguageName;
        if (_resources.ContainsKey(systemLang))
        {
            return CultureInfo.InstalledUICulture;
        }

        // Default to English
        return new CultureInfo("en");
    }

    /// <summary>
    /// Get localized string
    /// </summary>
    public string GetString(string key, params object[] args)
    {
        var lang = _currentCulture.TwoLetterISOLanguageName;

        if (_resources.TryGetValue(lang, out var langResources) &&
            langResources.TryGetValue(key, out var value))
        {
            return args.Length > 0 ? string.Format(value, args) : value;
        }

        // Fallback to English
        if (_resources.TryGetValue("en", out var enResources) &&
            enResources.TryGetValue(key, out var enValue))
        {
            return args.Length > 0 ? string.Format(enValue, args) : enValue;
        }

        // Return key if not found
        return key;
    }

    /// <summary>
    /// Get available cultures
    /// </summary>
    public IEnumerable<CultureInfo> GetAvailableCultures()
    {
        return _resources.Keys.Select(lang => new CultureInfo(lang));
    }

    /// <summary>
    /// Check if culture is supported
    /// </summary>
    public bool IsCultureSupported(CultureInfo culture)
    {
        return _resources.ContainsKey(culture.TwoLetterISOLanguageName);
    }
}
