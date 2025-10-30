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

        // Spanish resources
        _resources["es"] = new Dictionary<string, string>
        {
            ["Version"] = "Versión",
            ["HealthCheck"] = "Verificación de Salud",
            ["UpdateCheck"] = "Verificación de Actualización",
            ["EngineStarted"] = "Motor iniciado exitosamente",
            ["EngineStopped"] = "Motor detenido",
            ["InvalidCommand"] = "Comando inválido",
            ["CommandCompleted"] = "Comando completado exitosamente",
            ["ErrorOccurred"] = "Ocurrió un error",
            ["FileNotFound"] = "Archivo no encontrado",
            ["InvalidPath"] = "Ruta inválida",
            ["AccessDenied"] = "Acceso denegado",
            ["ConfigurationError"] = "Error de configuración",
            ["NetworkError"] = "Error de red",
            ["TimeoutError"] = "Operación expiró",
            ["ValidationError"] = "Error de validación",
            ["Success"] = "Éxito",
            ["Failed"] = "Fallido",
            ["Warning"] = "Advertencia",
            ["Info"] = "Información"
        };

        // German resources
        _resources["de"] = new Dictionary<string, string>
        {
            ["Version"] = "Version",
            ["HealthCheck"] = "Gesundheitsprüfung",
            ["UpdateCheck"] = "Update-Prüfung",
            ["EngineStarted"] = "Motor erfolgreich gestartet",
            ["EngineStopped"] = "Motor gestoppt",
            ["InvalidCommand"] = "Ungültiger Befehl",
            ["CommandCompleted"] = "Befehl erfolgreich abgeschlossen",
            ["ErrorOccurred"] = "Ein Fehler ist aufgetreten",
            ["FileNotFound"] = "Datei nicht gefunden",
            ["InvalidPath"] = "Ungültiger Pfad",
            ["AccessDenied"] = "Zugriff verweigert",
            ["ConfigurationError"] = "Konfigurationsfehler",
            ["NetworkError"] = "Netzwerkfehler",
            ["TimeoutError"] = "Vorgang ist abgelaufen",
            ["ValidationError"] = "Validierungsfehler",
            ["Success"] = "Erfolg",
            ["Failed"] = "Fehlgeschlagen",
            ["Warning"] = "Warnung",
            ["Info"] = "Information"
        };

        // French resources
        _resources["fr"] = new Dictionary<string, string>
        {
            ["Version"] = "Version",
            ["HealthCheck"] = "Vérification de Santé",
            ["UpdateCheck"] = "Vérification de Mise à Jour",
            ["EngineStarted"] = "Moteur démarré avec succès",
            ["EngineStopped"] = "Moteur arrêté",
            ["InvalidCommand"] = "Commande invalide",
            ["CommandCompleted"] = "Commande terminée avec succès",
            ["ErrorOccurred"] = "Une erreur s'est produite",
            ["FileNotFound"] = "Fichier non trouvé",
            ["InvalidPath"] = "Chemin invalide",
            ["AccessDenied"] = "Accès refusé",
            ["ConfigurationError"] = "Erreur de configuration",
            ["NetworkError"] = "Erreur réseau",
            ["TimeoutError"] = "Opération expirée",
            ["ValidationError"] = "Erreur de validation",
            ["Success"] = "Succès",
            ["Failed"] = "Échoué",
            ["Warning"] = "Avertissement",
            ["Info"] = "Information"
        };

        // Add more languages as needed for 50+ support
        // Example: Chinese, Korean, Arabic, etc.
        _resources["zh"] = new Dictionary<string, string>
        {
            ["Version"] = "版本",
            ["HealthCheck"] = "健康检查",
            ["UpdateCheck"] = "更新检查",
            ["EngineStarted"] = "引擎成功启动",
            ["EngineStopped"] = "引擎停止",
            ["InvalidCommand"] = "无效命令",
            ["CommandCompleted"] = "命令成功完成",
            ["ErrorOccurred"] = "发生错误",
            ["FileNotFound"] = "文件未找到",
            ["InvalidPath"] = "无效路径",
            ["AccessDenied"] = "访问被拒绝",
            ["ConfigurationError"] = "配置错误",
            ["NetworkError"] = "网络错误",
            ["TimeoutError"] = "操作超时",
            ["ValidationError"] = "验证错误",
            ["Success"] = "成功",
            ["Failed"] = "失败",
            ["Warning"] = "警告",
            ["Info"] = "信息"
        };

        _resources["ko"] = new Dictionary<string, string>
        {
            ["Version"] = "버전",
            ["HealthCheck"] = "건강 검사",
            ["UpdateCheck"] = "업데이트 확인",
            ["EngineStarted"] = "엔진이 성공적으로 시작됨",
            ["EngineStopped"] = "엔진 중지",
            ["InvalidCommand"] = "유효하지 않은 명령",
            ["CommandCompleted"] = "명령이 성공적으로 완료됨",
            ["ErrorOccurred"] = "오류가 발생했습니다",
            ["FileNotFound"] = "파일을 찾을 수 없습니다",
            ["InvalidPath"] = "유효하지 않은 경로",
            ["AccessDenied"] = "액세스 거부됨",
            ["ConfigurationError"] = "구성 오류",
            ["NetworkError"] = "네트워크 오류",
            ["TimeoutError"] = "작업 시간이 초과되었습니다",
            ["ValidationError"] = "검증 오류",
            ["Success"] = "성공",
            ["Failed"] = "실패",
            ["Warning"] = "경고",
            ["Info"] = "정보"
        };

        // Add RTL support for Arabic
        _resources["ar"] = new Dictionary<string, string>
        {
            ["Version"] = "الإصدار",
            ["HealthCheck"] = "فحص الصحة",
            ["UpdateCheck"] = "فحص التحديث",
            ["EngineStarted"] = "تم تشغيل المحرك بنجاح",
            ["EngineStopped"] = "تم إيقاف المحرك",
            ["InvalidCommand"] = "أمر غير صالح",
            ["CommandCompleted"] = "تم إكمال الأمر بنجاح",
            ["ErrorOccurred"] = "حدث خطأ",
            ["FileNotFound"] = "الملف غير موجود",
            ["InvalidPath"] = "مسار غير صالح",
            ["AccessDenied"] = "تم رفض الوصول",
            ["ConfigurationError"] = "خطأ في التكوين",
            ["NetworkError"] = "خطأ في الشبكة",
            ["TimeoutError"] = "انتهت مهلة العملية",
            ["ValidationError"] = "خطأ في التحقق",
            ["Success"] = "نجح",
            ["Failed"] = "فشل",
            ["Warning"] = "تحذير",
            ["Info"] = "معلومات"
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
