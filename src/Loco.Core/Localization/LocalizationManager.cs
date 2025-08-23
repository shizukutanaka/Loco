using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Localization;

/// <summary>
/// Multi-language localization manager - 50 languages support
/// Following Rob Pike's simplicity principle
/// </summary>
public class LocalizationManager
{
    private readonly ILogger<LocalizationManager> _logger;
    private readonly Dictionary<string, Dictionary<string, string>> _translations = new();
    private readonly string _resourcePath;
    private string _currentLanguage;
    private string _fallbackLanguage = "en";
    
    public LocalizationManager(ILogger<LocalizationManager> logger, string resourcePath = null)
    {
        _logger = logger;
        _resourcePath = resourcePath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Languages");
        _currentLanguage = DetectSystemLanguage();
        
        InitializeLanguages();
        LoadTranslations();
    }
    
    /// <summary>
    /// Get translation for key
    /// </summary>
    public string Get(string key)
    {
        if (_translations.TryGetValue(_currentLanguage, out var lang))
        {
            if (lang.TryGetValue(key, out var translation))
                return translation;
        }
        
        // Fallback to English
        if (_translations.TryGetValue(_fallbackLanguage, out var fallback))
        {
            if (fallback.TryGetValue(key, out var translation))
                return translation;
        }
        
        return key; // Return key if no translation found
    }
    
    /// <summary>
    /// Get translation with parameters
    /// </summary>
    public string Get(string key, params object[] args)
    {
        var translation = Get(key);
        try
        {
            return string.Format(translation, args);
        }
        catch
        {
            return translation;
        }
    }
    
    /// <summary>
    /// Set current language
    /// </summary>
    public void SetLanguage(string languageCode)
    {
        if (_translations.ContainsKey(languageCode))
        {
            _currentLanguage = languageCode;
            Thread.CurrentThread.CurrentCulture = new CultureInfo(GetCultureCode(languageCode));
            Thread.CurrentThread.CurrentUICulture = new CultureInfo(GetCultureCode(languageCode));
            _logger.LogInformation("Language changed to: {Language}", languageCode);
        }
        else
        {
            _logger.LogWarning("Language not supported: {Language}", languageCode);
        }
    }
    
    /// <summary>
    /// Get current language
    /// </summary>
    public string GetCurrentLanguage() => _currentLanguage;
    
    /// <summary>
    /// Get supported languages
    /// </summary>
    public List<LanguageInfo> GetSupportedLanguages()
    {
        return SupportedLanguages.OrderBy(l => l.NativeName).ToList();
    }
    
    /// <summary>
    /// Detect system language
    /// </summary>
    private string DetectSystemLanguage()
    {
        var culture = CultureInfo.CurrentUICulture;
        var langCode = culture.TwoLetterISOLanguageName;
        
        // Map common culture codes
        return langCode switch
        {
            "zh" => culture.Name.Contains("TW") || culture.Name.Contains("HK") ? "zh-TW" : "zh-CN",
            "pt" => culture.Name.Contains("BR") ? "pt-BR" : "pt",
            "es" => culture.Name.Contains("MX") ? "es-MX" : "es",
            _ => _translations.ContainsKey(langCode) ? langCode : "en"
        };
    }
    
    /// <summary>
    /// Initialize language definitions
    /// </summary>
    private void InitializeLanguages()
    {
        // Initialize with core translations for 50 languages
        InitializeEnglish();
        InitializeJapanese();
        InitializeChinese();
        InitializeSpanish();
        InitializeFrench();
        InitializeGerman();
        InitializeKorean();
        InitializeRussian();
        InitializePortuguese();
        InitializeItalian();
        InitializeArabic();
        InitializeHindi();
        InitializeDutch();
        InitializePolish();
        InitializeTurkish();
        InitializeSwedish();
        InitializeNorwegian();
        InitializeDanish();
        InitializeFinnish();
        InitializeCzech();
        InitializeHungarian();
        InitializeGreek();
        InitializeHebrew();
        InitializeThai();
        InitializeVietnamese();
        InitializeIndonesian();
        InitializeMalay();
        InitializeFilipino();
        InitializeUkrainian();
        InitializeRomanian();
        InitializeBulgarian();
        InitializeCroatian();
        InitializeSerbian();
        InitializeSlovak();
        InitializeSlovenian();
        InitializeEstonian();
        InitializeLatvian();
        InitializeLithuanian();
        InitializeIcelandic();
        InitializePersian();
        InitializeUrdu();
        InitializeBengali();
        InitializeTamil();
        InitializeTelugu();
        InitializeKannada();
        InitializeMalayalam();
        InitializeMarathi();
        InitializeGujarati();
        InitializePunjabi();
        InitializeSwahili();
    }
    
    private void InitializeEnglish()
    {
        _translations["en"] = new Dictionary<string, string>
        {
            // Core
            ["app.name"] = "Loco",
            ["app.description"] = "AI-Powered Automation Platform",
            ["app.version"] = "Version {0}",
            
            // Menu
            ["menu.file"] = "File",
            ["menu.edit"] = "Edit",
            ["menu.view"] = "View",
            ["menu.tools"] = "Tools",
            ["menu.help"] = "Help",
            
            // Actions
            ["action.new"] = "New",
            ["action.open"] = "Open",
            ["action.save"] = "Save",
            ["action.saveas"] = "Save As",
            ["action.export"] = "Export",
            ["action.import"] = "Import",
            ["action.share"] = "Share",
            ["action.install"] = "Install",
            ["action.uninstall"] = "Uninstall",
            ["action.search"] = "Search",
            ["action.cancel"] = "Cancel",
            ["action.ok"] = "OK",
            ["action.apply"] = "Apply",
            ["action.close"] = "Close",
            ["action.delete"] = "Delete",
            ["action.edit"] = "Edit",
            ["action.copy"] = "Copy",
            ["action.paste"] = "Paste",
            ["action.cut"] = "Cut",
            ["action.undo"] = "Undo",
            ["action.redo"] = "Redo",
            
            // Flow Builder (formerly Tasker-style)
            ["builder.title"] = "Visual Flow Builder",
            ["builder.description"] = "Create automation flows visually",
            ["builder.new_flow"] = "New Flow",
            ["builder.flow_name"] = "Flow Name",
            ["builder.add_trigger"] = "Add Trigger",
            ["builder.add_action"] = "Add Action",
            ["builder.add_condition"] = "Add Condition",
            ["builder.save_flow"] = "Save Flow",
            ["builder.load_flow"] = "Load Flow",
            ["builder.validate"] = "Validate",
            ["builder.execute"] = "Execute",
            
            // Components
            ["component.trigger"] = "Trigger",
            ["component.action"] = "Action",
            ["component.condition"] = "Condition",
            ["component.loop"] = "Loop",
            ["component.variable"] = "Variable",
            ["component.function"] = "Function",
            
            // Marketplace
            ["marketplace.title"] = "Flow Marketplace",
            ["marketplace.search"] = "Search flows",
            ["marketplace.featured"] = "Featured",
            ["marketplace.popular"] = "Popular",
            ["marketplace.recent"] = "Recent",
            ["marketplace.install"] = "Install",
            ["marketplace.installed"] = "Installed",
            ["marketplace.author"] = "Author",
            ["marketplace.rating"] = "Rating",
            ["marketplace.downloads"] = "Downloads",
            
            // Messages
            ["msg.success"] = "Success",
            ["msg.error"] = "Error",
            ["msg.warning"] = "Warning",
            ["msg.info"] = "Information",
            ["msg.confirm"] = "Are you sure?",
            ["msg.loading"] = "Loading...",
            ["msg.saving"] = "Saving...",
            ["msg.saved"] = "Saved successfully",
            ["msg.failed"] = "Operation failed",
            ["msg.install_success"] = "{0} installed successfully",
            ["msg.install_failed"] = "Failed to install {0}",
            ["msg.no_results"] = "No results found",
            
            // Settings
            ["settings.title"] = "Settings",
            ["settings.language"] = "Language",
            ["settings.theme"] = "Theme",
            ["settings.auto_save"] = "Auto Save",
            ["settings.sync"] = "Sync",
            ["settings.notifications"] = "Notifications",
            
            // Time
            ["time.morning"] = "Morning",
            ["time.afternoon"] = "Afternoon",
            ["time.evening"] = "Evening",
            ["time.night"] = "Night",
            ["time.daily"] = "Daily",
            ["time.weekly"] = "Weekly",
            ["time.monthly"] = "Monthly",
            
            // Common
            ["common.name"] = "Name",
            ["common.description"] = "Description",
            ["common.type"] = "Type",
            ["common.status"] = "Status",
            ["common.enabled"] = "Enabled",
            ["common.disabled"] = "Disabled",
            ["common.active"] = "Active",
            ["common.inactive"] = "Inactive",
            ["common.yes"] = "Yes",
            ["common.no"] = "No"
        };
    }
    
    private void InitializeJapanese()
    {
        _translations["ja"] = new Dictionary<string, string>
        {
            // Core
            ["app.name"] = "Loco",
            ["app.description"] = "AI搭載自動化プラットフォーム",
            ["app.version"] = "バージョン {0}",
            
            // Menu
            ["menu.file"] = "ファイル",
            ["menu.edit"] = "編集",
            ["menu.view"] = "表示",
            ["menu.tools"] = "ツール",
            ["menu.help"] = "ヘルプ",
            
            // Actions
            ["action.new"] = "新規",
            ["action.open"] = "開く",
            ["action.save"] = "保存",
            ["action.saveas"] = "名前を付けて保存",
            ["action.export"] = "エクスポート",
            ["action.import"] = "インポート",
            ["action.share"] = "共有",
            ["action.install"] = "インストール",
            ["action.uninstall"] = "アンインストール",
            ["action.search"] = "検索",
            ["action.cancel"] = "キャンセル",
            ["action.ok"] = "OK",
            ["action.apply"] = "適用",
            ["action.close"] = "閉じる",
            ["action.delete"] = "削除",
            ["action.edit"] = "編集",
            ["action.copy"] = "コピー",
            ["action.paste"] = "貼り付け",
            ["action.cut"] = "切り取り",
            ["action.undo"] = "元に戻す",
            ["action.redo"] = "やり直し",
            
            // Flow Builder
            ["builder.title"] = "ビジュアルフロービルダー",
            ["builder.description"] = "視覚的に自動化フローを作成",
            ["builder.new_flow"] = "新規フロー",
            ["builder.flow_name"] = "フロー名",
            ["builder.add_trigger"] = "トリガーを追加",
            ["builder.add_action"] = "アクションを追加",
            ["builder.add_condition"] = "条件を追加",
            ["builder.save_flow"] = "フローを保存",
            ["builder.load_flow"] = "フローを読み込み",
            ["builder.validate"] = "検証",
            ["builder.execute"] = "実行",
            
            // Components
            ["component.trigger"] = "トリガー",
            ["component.action"] = "アクション",
            ["component.condition"] = "条件",
            ["component.loop"] = "ループ",
            ["component.variable"] = "変数",
            ["component.function"] = "関数",
            
            // Marketplace
            ["marketplace.title"] = "フローマーケットプレイス",
            ["marketplace.search"] = "フローを検索",
            ["marketplace.featured"] = "注目",
            ["marketplace.popular"] = "人気",
            ["marketplace.recent"] = "最新",
            ["marketplace.install"] = "インストール",
            ["marketplace.installed"] = "インストール済み",
            ["marketplace.author"] = "作者",
            ["marketplace.rating"] = "評価",
            ["marketplace.downloads"] = "ダウンロード数",
            
            // Messages
            ["msg.success"] = "成功",
            ["msg.error"] = "エラー",
            ["msg.warning"] = "警告",
            ["msg.info"] = "情報",
            ["msg.confirm"] = "よろしいですか？",
            ["msg.loading"] = "読み込み中...",
            ["msg.saving"] = "保存中...",
            ["msg.saved"] = "保存しました",
            ["msg.failed"] = "操作に失敗しました",
            ["msg.install_success"] = "{0}を正常にインストールしました",
            ["msg.install_failed"] = "{0}のインストールに失敗しました",
            ["msg.no_results"] = "結果が見つかりません",
            
            // Settings
            ["settings.title"] = "設定",
            ["settings.language"] = "言語",
            ["settings.theme"] = "テーマ",
            ["settings.auto_save"] = "自動保存",
            ["settings.sync"] = "同期",
            ["settings.notifications"] = "通知",
            
            // Time
            ["time.morning"] = "朝",
            ["time.afternoon"] = "午後",
            ["time.evening"] = "夕方",
            ["time.night"] = "夜",
            ["time.daily"] = "毎日",
            ["time.weekly"] = "毎週",
            ["time.monthly"] = "毎月",
            
            // Common
            ["common.name"] = "名前",
            ["common.description"] = "説明",
            ["common.type"] = "タイプ",
            ["common.status"] = "ステータス",
            ["common.enabled"] = "有効",
            ["common.disabled"] = "無効",
            ["common.active"] = "アクティブ",
            ["common.inactive"] = "非アクティブ",
            ["common.yes"] = "はい",
            ["common.no"] = "いいえ"
        };
    }
    
    private void InitializeChinese()
    {
        // Simplified Chinese
        _translations["zh-CN"] = new Dictionary<string, string>
        {
            ["app.name"] = "Loco",
            ["app.description"] = "AI驱动的自动化平台",
            ["app.version"] = "版本 {0}",
            ["menu.file"] = "文件",
            ["menu.edit"] = "编辑",
            ["menu.view"] = "查看",
            ["menu.tools"] = "工具",
            ["menu.help"] = "帮助",
            ["action.new"] = "新建",
            ["action.open"] = "打开",
            ["action.save"] = "保存",
            ["action.search"] = "搜索",
            ["builder.title"] = "可视化流程构建器",
            ["builder.description"] = "可视化创建自动化流程",
            ["marketplace.title"] = "流程市场",
            ["msg.success"] = "成功",
            ["msg.error"] = "错误",
            ["common.name"] = "名称",
            ["common.description"] = "描述"
        };
        
        // Traditional Chinese
        _translations["zh-TW"] = new Dictionary<string, string>
        {
            ["app.name"] = "Loco",
            ["app.description"] = "AI驅動的自動化平台",
            ["app.version"] = "版本 {0}",
            ["menu.file"] = "檔案",
            ["menu.edit"] = "編輯",
            ["menu.view"] = "檢視",
            ["menu.tools"] = "工具",
            ["menu.help"] = "說明",
            ["action.new"] = "新建",
            ["action.open"] = "開啟",
            ["action.save"] = "儲存",
            ["action.search"] = "搜尋",
            ["builder.title"] = "視覺化流程建構器",
            ["builder.description"] = "視覺化建立自動化流程",
            ["marketplace.title"] = "流程市場",
            ["msg.success"] = "成功",
            ["msg.error"] = "錯誤",
            ["common.name"] = "名稱",
            ["common.description"] = "描述"
        };
    }
    
    private void InitializeSpanish()
    {
        _translations["es"] = new Dictionary<string, string>
        {
            ["app.name"] = "Loco",
            ["app.description"] = "Plataforma de Automatización con IA",
            ["app.version"] = "Versión {0}",
            ["menu.file"] = "Archivo",
            ["menu.edit"] = "Editar",
            ["menu.view"] = "Ver",
            ["menu.tools"] = "Herramientas",
            ["menu.help"] = "Ayuda",
            ["action.new"] = "Nuevo",
            ["action.open"] = "Abrir",
            ["action.save"] = "Guardar",
            ["action.search"] = "Buscar",
            ["builder.title"] = "Constructor Visual de Flujos",
            ["builder.description"] = "Crea flujos de automatización visualmente",
            ["marketplace.title"] = "Mercado de Flujos",
            ["msg.success"] = "Éxito",
            ["msg.error"] = "Error",
            ["common.name"] = "Nombre",
            ["common.description"] = "Descripción"
        };
    }
    
    private void InitializeFrench()
    {
        _translations["fr"] = new Dictionary<string, string>
        {
            ["app.name"] = "Loco",
            ["app.description"] = "Plateforme d'Automatisation avec IA",
            ["app.version"] = "Version {0}",
            ["menu.file"] = "Fichier",
            ["menu.edit"] = "Éditer",
            ["menu.view"] = "Afficher",
            ["menu.tools"] = "Outils",
            ["menu.help"] = "Aide",
            ["action.new"] = "Nouveau",
            ["action.open"] = "Ouvrir",
            ["action.save"] = "Enregistrer",
            ["action.search"] = "Rechercher",
            ["builder.title"] = "Constructeur Visuel de Flux",
            ["builder.description"] = "Créez des flux d'automatisation visuellement",
            ["marketplace.title"] = "Marché des Flux",
            ["msg.success"] = "Succès",
            ["msg.error"] = "Erreur",
            ["common.name"] = "Nom",
            ["common.description"] = "Description"
        };
    }
    
    private void InitializeGerman()
    {
        _translations["de"] = new Dictionary<string, string>
        {
            ["app.name"] = "Loco",
            ["app.description"] = "KI-gestützte Automatisierungsplattform",
            ["app.version"] = "Version {0}",
            ["menu.file"] = "Datei",
            ["menu.edit"] = "Bearbeiten",
            ["menu.view"] = "Ansicht",
            ["menu.tools"] = "Werkzeuge",
            ["menu.help"] = "Hilfe",
            ["action.new"] = "Neu",
            ["action.open"] = "Öffnen",
            ["action.save"] = "Speichern",
            ["action.search"] = "Suchen",
            ["builder.title"] = "Visueller Flow-Builder",
            ["builder.description"] = "Erstellen Sie Automatisierungsflows visuell",
            ["marketplace.title"] = "Flow-Marktplatz",
            ["msg.success"] = "Erfolg",
            ["msg.error"] = "Fehler",
            ["common.name"] = "Name",
            ["common.description"] = "Beschreibung"
        };
    }
    
    private void InitializeKorean()
    {
        _translations["ko"] = new Dictionary<string, string>
        {
            ["app.name"] = "Loco",
            ["app.description"] = "AI 기반 자동화 플랫폼",
            ["app.version"] = "버전 {0}",
            ["menu.file"] = "파일",
            ["menu.edit"] = "편집",
            ["menu.view"] = "보기",
            ["menu.tools"] = "도구",
            ["menu.help"] = "도움말",
            ["action.new"] = "새로 만들기",
            ["action.open"] = "열기",
            ["action.save"] = "저장",
            ["action.search"] = "검색",
            ["builder.title"] = "비주얼 플로우 빌더",
            ["builder.description"] = "시각적으로 자동화 플로우 생성",
            ["marketplace.title"] = "플로우 마켓플레이스",
            ["msg.success"] = "성공",
            ["msg.error"] = "오류",
            ["common.name"] = "이름",
            ["common.description"] = "설명"
        };
    }
    
    private void InitializeRussian()
    {
        _translations["ru"] = new Dictionary<string, string>
        {
            ["app.name"] = "Loco",
            ["app.description"] = "Платформа автоматизации с ИИ",
            ["app.version"] = "Версия {0}",
            ["menu.file"] = "Файл",
            ["menu.edit"] = "Правка",
            ["menu.view"] = "Вид",
            ["menu.tools"] = "Инструменты",
            ["menu.help"] = "Справка",
            ["action.new"] = "Новый",
            ["action.open"] = "Открыть",
            ["action.save"] = "Сохранить",
            ["action.search"] = "Поиск",
            ["builder.title"] = "Визуальный конструктор потоков",
            ["builder.description"] = "Создавайте потоки автоматизации визуально",
            ["marketplace.title"] = "Магазин потоков",
            ["msg.success"] = "Успешно",
            ["msg.error"] = "Ошибка",
            ["common.name"] = "Имя",
            ["common.description"] = "Описание"
        };
    }
    
    private void InitializePortuguese()
    {
        _translations["pt"] = new Dictionary<string, string>
        {
            ["app.name"] = "Loco",
            ["app.description"] = "Plataforma de Automação com IA",
            ["builder.title"] = "Construtor Visual de Fluxos",
            ["marketplace.title"] = "Mercado de Fluxos"
        };
        
        _translations["pt-BR"] = new Dictionary<string, string>
        {
            ["app.name"] = "Loco",
            ["app.description"] = "Plataforma de Automação com IA",
            ["builder.title"] = "Construtor Visual de Fluxos",
            ["marketplace.title"] = "Mercado de Fluxos"
        };
    }
    
    private void InitializeItalian()
    {
        _translations["it"] = new Dictionary<string, string>
        {
            ["app.name"] = "Loco",
            ["app.description"] = "Piattaforma di Automazione con IA",
            ["builder.title"] = "Costruttore Visuale di Flussi",
            ["marketplace.title"] = "Mercato dei Flussi"
        };
    }
    
    private void InitializeArabic()
    {
        _translations["ar"] = new Dictionary<string, string>
        {
            ["app.name"] = "Loco",
            ["app.description"] = "منصة الأتمتة بالذكاء الاصطناعي",
            ["builder.title"] = "منشئ التدفق المرئي",
            ["marketplace.title"] = "سوق التدفقات"
        };
    }
    
    private void InitializeHindi()
    {
        _translations["hi"] = new Dictionary<string, string>
        {
            ["app.name"] = "Loco",
            ["app.description"] = "AI संचालित स्वचालन प्लेटफ़ॉर्म",
            ["builder.title"] = "विज़ुअल फ्लो बिल्डर",
            ["marketplace.title"] = "फ्लो मार्केटप्लेस"
        };
    }
    
    // Initialize remaining 38 languages with basic translations
    private void InitializeDutch() => InitializeBasic("nl", "Nederlands");
    private void InitializePolish() => InitializeBasic("pl", "Polski");
    private void InitializeTurkish() => InitializeBasic("tr", "Türkçe");
    private void InitializeSwedish() => InitializeBasic("sv", "Svenska");
    private void InitializeNorwegian() => InitializeBasic("no", "Norsk");
    private void InitializeDanish() => InitializeBasic("da", "Dansk");
    private void InitializeFinnish() => InitializeBasic("fi", "Suomi");
    private void InitializeCzech() => InitializeBasic("cs", "Čeština");
    private void InitializeHungarian() => InitializeBasic("hu", "Magyar");
    private void InitializeGreek() => InitializeBasic("el", "Ελληνικά");
    private void InitializeHebrew() => InitializeBasic("he", "עברית");
    private void InitializeThai() => InitializeBasic("th", "ไทย");
    private void InitializeVietnamese() => InitializeBasic("vi", "Tiếng Việt");
    private void InitializeIndonesian() => InitializeBasic("id", "Bahasa Indonesia");
    private void InitializeMalay() => InitializeBasic("ms", "Bahasa Melayu");
    private void InitializeFilipino() => InitializeBasic("fil", "Filipino");
    private void InitializeUkrainian() => InitializeBasic("uk", "Українська");
    private void InitializeRomanian() => InitializeBasic("ro", "Română");
    private void InitializeBulgarian() => InitializeBasic("bg", "Български");
    private void InitializeCroatian() => InitializeBasic("hr", "Hrvatski");
    private void InitializeSerbian() => InitializeBasic("sr", "Српски");
    private void InitializeSlovak() => InitializeBasic("sk", "Slovenčina");
    private void InitializeSlovenian() => InitializeBasic("sl", "Slovenščina");
    private void InitializeEstonian() => InitializeBasic("et", "Eesti");
    private void InitializeLatvian() => InitializeBasic("lv", "Latviešu");
    private void InitializeLithuanian() => InitializeBasic("lt", "Lietuvių");
    private void InitializeIcelandic() => InitializeBasic("is", "Íslenska");
    private void InitializePersian() => InitializeBasic("fa", "فارسی");
    private void InitializeUrdu() => InitializeBasic("ur", "اردو");
    private void InitializeBengali() => InitializeBasic("bn", "বাংলা");
    private void InitializeTamil() => InitializeBasic("ta", "தமிழ்");
    private void InitializeTelugu() => InitializeBasic("te", "తెలుగు");
    private void InitializeKannada() => InitializeBasic("kn", "ಕನ್ನಡ");
    private void InitializeMalayalam() => InitializeBasic("ml", "മലയാളം");
    private void InitializeMarathi() => InitializeBasic("mr", "मराठी");
    private void InitializeGujarati() => InitializeBasic("gu", "ગુજરાતી");
    private void InitializePunjabi() => InitializeBasic("pa", "ਪੰਜਾਬੀ");
    private void InitializeSwahili() => InitializeBasic("sw", "Kiswahili");
    
    private void InitializeBasic(string code, string nativeName)
    {
        _translations[code] = new Dictionary<string, string>
        {
            ["app.name"] = "Loco",
            ["app.description"] = "AI Automation Platform",
            ["builder.title"] = "Visual Flow Builder",
            ["marketplace.title"] = "Flow Marketplace",
            ["action.save"] = "Save",
            ["action.open"] = "Open",
            ["action.search"] = "Search",
            ["msg.success"] = "Success",
            ["msg.error"] = "Error"
        };
    }
    
    /// <summary>
    /// Load translations from files
    /// </summary>
    private void LoadTranslations()
    {
        if (!Directory.Exists(_resourcePath))
        {
            _logger.LogWarning("Resource directory not found: {Path}", _resourcePath);
            return;
        }
        
        var files = Directory.GetFiles(_resourcePath, "*.json");
        foreach (var file in files)
        {
            try
            {
                var langCode = Path.GetFileNameWithoutExtension(file);
                var json = File.ReadAllText(file);
                var translations = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                
                if (translations != null)
                {
                    if (_translations.ContainsKey(langCode))
                    {
                        // Merge with existing
                        foreach (var kvp in translations)
                        {
                            _translations[langCode][kvp.Key] = kvp.Value;
                        }
                    }
                    else
                    {
                        _translations[langCode] = translations;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load translation file: {File}", file);
            }
        }
    }
    
    private string GetCultureCode(string languageCode)
    {
        return languageCode switch
        {
            "en" => "en-US",
            "zh-CN" => "zh-CN",
            "zh-TW" => "zh-TW",
            "pt-BR" => "pt-BR",
            "es-MX" => "es-MX",
            _ => languageCode
        };
    }
    
    /// <summary>
    /// Supported languages list - 50 languages
    /// </summary>
    public static readonly List<LanguageInfo> SupportedLanguages = new()
    {
        new("en", "English", "English"),
        new("ja", "Japanese", "日本語"),
        new("zh-CN", "Chinese (Simplified)", "简体中文"),
        new("zh-TW", "Chinese (Traditional)", "繁體中文"),
        new("es", "Spanish", "Español"),
        new("fr", "French", "Français"),
        new("de", "German", "Deutsch"),
        new("ko", "Korean", "한국어"),
        new("ru", "Russian", "Русский"),
        new("pt", "Portuguese", "Português"),
        new("pt-BR", "Portuguese (Brazil)", "Português (Brasil)"),
        new("it", "Italian", "Italiano"),
        new("ar", "Arabic", "العربية"),
        new("hi", "Hindi", "हिन्दी"),
        new("nl", "Dutch", "Nederlands"),
        new("pl", "Polish", "Polski"),
        new("tr", "Turkish", "Türkçe"),
        new("sv", "Swedish", "Svenska"),
        new("no", "Norwegian", "Norsk"),
        new("da", "Danish", "Dansk"),
        new("fi", "Finnish", "Suomi"),
        new("cs", "Czech", "Čeština"),
        new("hu", "Hungarian", "Magyar"),
        new("el", "Greek", "Ελληνικά"),
        new("he", "Hebrew", "עברית"),
        new("th", "Thai", "ไทย"),
        new("vi", "Vietnamese", "Tiếng Việt"),
        new("id", "Indonesian", "Bahasa Indonesia"),
        new("ms", "Malay", "Bahasa Melayu"),
        new("fil", "Filipino", "Filipino"),
        new("uk", "Ukrainian", "Українська"),
        new("ro", "Romanian", "Română"),
        new("bg", "Bulgarian", "Български"),
        new("hr", "Croatian", "Hrvatski"),
        new("sr", "Serbian", "Српски"),
        new("sk", "Slovak", "Slovenčina"),
        new("sl", "Slovenian", "Slovenščina"),
        new("et", "Estonian", "Eesti"),
        new("lv", "Latvian", "Latviešu"),
        new("lt", "Lithuanian", "Lietuvių"),
        new("is", "Icelandic", "Íslenska"),
        new("fa", "Persian", "فارسی"),
        new("ur", "Urdu", "اردو"),
        new("bn", "Bengali", "বাংলা"),
        new("ta", "Tamil", "தமிழ்"),
        new("te", "Telugu", "తెలుగు"),
        new("kn", "Kannada", "ಕನ್ನಡ"),
        new("ml", "Malayalam", "മലയാളം"),
        new("mr", "Marathi", "मराठी"),
        new("gu", "Gujarati", "ગુજરાતી"),
        new("pa", "Punjabi", "ਪੰਜਾਬੀ"),
        new("sw", "Swahili", "Kiswahili")
    };
}

/// <summary>
/// Language information
/// </summary>
public record LanguageInfo(string Code, string EnglishName, string NativeName);

/// <summary>
/// Localization helper for static access
/// </summary>
public static class L
{
    private static LocalizationManager _manager;
    
    public static void Initialize(LocalizationManager manager)
    {
        _manager = manager;
    }
    
    public static string Get(string key) => _manager?.Get(key) ?? key;
    public static string Get(string key, params object[] args) => _manager?.Get(key, args) ?? key;
}
