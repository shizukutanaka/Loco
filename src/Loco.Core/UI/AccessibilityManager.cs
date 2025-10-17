using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Loco.Core.UI;

/// <summary>
/// アクセシビリティマネージャー
/// </summary>
public class AccessibilityManager
{
    private AccessibilitySettings _currentSettings;
    private readonly Dictionary<string, AccessibilityProfile> _profiles = new();

    public AccessibilityManager()
    {
        InitializeDefaultProfiles();
        LoadSettings();
    }

    /// <summary>
    /// 現在のアクセシビリティ設定を取得
    /// </summary>
    public AccessibilitySettings CurrentSettings => _currentSettings;

    /// <summary>
    /// アクセシビリティプロファイルを取得
    /// </summary>
    public IEnumerable<AccessibilityProfile> GetProfiles()
    {
        return _profiles.Values.OrderBy(p => p.Name);
    }

    /// <summary>
    /// プロファイルを適用
    /// </summary>
    public async Task ApplyProfileAsync(string profileId)
    {
        if (_profiles.TryGetValue(profileId, out var profile))
        {
            _currentSettings = profile.Settings;
            await SaveSettingsAsync();
            await OnSettingsChangedAsync();
        }
    }

    /// <summary>
    /// 設定を更新
    /// </summary>
    public async Task UpdateSettingsAsync(AccessibilitySettings settings)
    {
        _currentSettings = settings;
        await SaveSettingsAsync();
        await OnSettingsChangedAsync();
    }

    /// <summary>
    /// キーボードショートカットを取得
    /// </summary>
    public IEnumerable<KeyboardShortcut> GetKeyboardShortcuts(string context = null)
    {
        var shortcuts = new List<KeyboardShortcut>();

        // グローバルショートカット
        shortcuts.AddRange(new[]
        {
            new KeyboardShortcut { Key = "F1", Modifiers = ModifierKeys.None, Action = "ShowHelp", Description = "Show help", DescriptionJa = "ヘルプを表示" },
            new KeyboardShortcut { Key = "Ctrl+N", Modifiers = ModifierKeys.Control, Action = "NewWorkflow", Description = "Create new workflow", DescriptionJa = "新しいワークフローを作成" },
            new KeyboardShortcut { Key = "Ctrl+O", Modifiers = ModifierKeys.Control, Action = "OpenWorkflow", Description = "Open workflow", DescriptionJa = "ワークフローを開く" },
            new KeyboardShortcut { Key = "Ctrl+S", Modifiers = ModifierKeys.Control, Action = "SaveWorkflow", Description = "Save workflow", DescriptionJa = "ワークフローを保存" },
            new KeyboardShortcut { Key = "Ctrl+Z", Modifiers = ModifierKeys.Control, Action = "Undo", Description = "Undo last action", DescriptionJa = "最後の操作を元に戻す" },
            new KeyboardShortcut { Key = "Ctrl+Y", Modifiers = ModifierKeys.Control, Action = "Redo", Description = "Redo last action", DescriptionJa = "最後の操作をやり直す" },
            new KeyboardShortcut { Key = "F11", Modifiers = ModifierKeys.None, Action = "ToggleFullscreen", Description = "Toggle fullscreen", DescriptionJa = "全画面表示を切り替え" },
            new KeyboardShortcut { Key = "Alt+F4", Modifiers = ModifierKeys.Alt, Action = "Exit", Description = "Exit application", DescriptionJa = "アプリケーションを終了" }
        });

        // コンテキスト固有のショートカット
        if (context == "WorkflowEditor")
        {
            shortcuts.AddRange(new[]
            {
                new KeyboardShortcut { Key = "Delete", Modifiers = ModifierKeys.None, Action = "DeleteSelected", Description = "Delete selected items", DescriptionJa = "選択したアイテムを削除" },
                new KeyboardShortcut { Key = "Ctrl+A", Modifiers = ModifierKeys.Control, Action = "SelectAll", Description = "Select all items", DescriptionJa = "すべてのアイテムを選択" },
                new KeyboardShortcut { Key = "Ctrl+C", Modifiers = ModifierKeys.Control, Action = "Copy", Description = "Copy selected items", DescriptionJa = "選択したアイテムをコピー" },
                new KeyboardShortcut { Key = "Ctrl+V", Modifiers = ModifierKeys.Control, Action = "Paste", Description = "Paste items", DescriptionJa = "アイテムを貼り付け" },
                new KeyboardShortcut { Key = "F5", Modifiers = ModifierKeys.None, Action = "RunWorkflow", Description = "Run current workflow", DescriptionJa = "現在のワークフローを実行" }
            });
        }

        return shortcuts.Where(s => !_currentSettings.DisabledShortcuts.Contains(s.Action));
    }

    /// <summary>
    /// スクリーンリーダーに適したテキストを取得
    /// </summary>
    public ScreenReaderText GetScreenReaderText(string elementId, string defaultText, string context = null)
    {
        var text = new ScreenReaderText
        {
            ElementId = elementId,
            PrimaryText = defaultText,
            Context = context
        };

        // 詳細な説明を追加
        switch (elementId)
        {
            case "workflow-save-button":
                text.PrimaryText = "Save current workflow";
                text.SecondaryText = "Saves the current workflow configuration to disk";
                text.AccessKey = "S";
                break;

            case "workflow-run-button":
                text.PrimaryText = "Execute workflow";
                text.SecondaryText = "Runs the current workflow and shows results";
                text.AccessKey = "R";
                break;

            case "settings-dialog":
                text.PrimaryText = "Application settings";
                text.SecondaryText = "Configure application preferences and options";
                break;
        }

        // 高コントラストモードの場合は追加の情報を提供
        if (_currentSettings.HighContrastMode)
        {
            text.AdditionalInfo = "High contrast mode is enabled";
        }

        // 拡大モードの場合はフォント情報を提供
        if (_currentSettings.TextScaling > 1.0)
        {
            text.AdditionalInfo = $"Text is scaled to {_currentSettings.TextScaling * 100:F0}%";
        }

        return text;
    }

    /// <summary>
    /// フォーカス管理
    /// </summary>
    public FocusNavigation GetFocusNavigation(string currentElement, NavigationDirection direction)
    {
        var navigation = new FocusNavigation
        {
            CurrentElement = currentElement,
            Direction = direction
        };

        // 論理的なフォーカス順序を定義
        var focusOrder = new Dictionary<string, string[]>
        {
            ["main-window"] = new[] { "menu-bar", "toolbar", "main-content", "status-bar" },
            ["workflow-editor"] = new[] { "workflow-name", "trigger-section", "action-list", "save-button", "run-button" },
            ["settings-dialog"] = new[] { "general-tab", "accessibility-tab", "advanced-tab", "ok-button", "cancel-button" }
        };

        if (focusOrder.TryGetValue(currentElement, out var order))
        {
            var currentIndex = Array.IndexOf(order, currentElement);
            if (currentIndex >= 0)
            {
                int nextIndex;
                switch (direction)
                {
                    case NavigationDirection.Next:
                        nextIndex = (currentIndex + 1) % order.Length;
                        break;
                    case NavigationDirection.Previous:
                        nextIndex = (currentIndex - 1 + order.Length) % order.Length;
                        break;
                    case NavigationDirection.First:
                        nextIndex = 0;
                        break;
                    case NavigationDirection.Last:
                        nextIndex = order.Length - 1;
                        break;
                    default:
                        nextIndex = currentIndex;
                        break;
                }

                navigation.NextElement = order[nextIndex];
                navigation.IsValid = true;
            }
        }

        return navigation;
    }

    /// <summary>
    /// 色コントラストを検証
    /// </summary>
    public ColorContrastValidation ValidateColorContrast(System.Drawing.Color foreground, System.Drawing.Color background)
    {
        // WCAG 2.1のコントラスト比を計算
        var contrastRatio = CalculateContrastRatio(foreground, background);

        var validation = new ColorContrastValidation
        {
            ForegroundColor = foreground,
            BackgroundColor = background,
            ContrastRatio = contrastRatio
        };

        // WCAG 2.1 AA基準
        validation.AALargeText = contrastRatio >= 3.0; // 大きなテキスト（18pt以上、または太字14pt以上）
        validation.AANormalText = contrastRatio >= 4.5; // 通常のテキスト

        // WCAG 2.1 AAA基準
        validation.AAALargeText = contrastRatio >= 4.5;
        validation.AAANormalText = contrastRatio >= 7.0;

        return validation;
    }

    /// <summary>
    /// アクセシビリティチェックを実行
    /// </summary>
    public async Task<AccessibilityAuditResult> PerformAccessibilityAuditAsync()
    {
        var result = new AccessibilityAuditResult
        {
            AuditTime = DateTime.UtcNow
        };

        // 現在の設定をチェック
        if (_currentSettings.ScreenReaderEnabled)
        {
            result.PassedChecks.Add("Screen reader support enabled");
        }
        else
        {
            result.FailedChecks.Add("Screen reader support not enabled");
        }

        if (_currentSettings.HighContrastMode)
        {
            result.PassedChecks.Add("High contrast mode enabled");
        }
        else
        {
            result.FailedChecks.Add("High contrast mode not enabled - consider enabling for better visibility");
        }

        if (_currentSettings.TextScaling >= 1.0)
        {
            result.PassedChecks.Add($"Text scaling is set to {_currentSettings.TextScaling * 100:F0}%");
        }

        if (_currentSettings.ReduceMotion)
        {
            result.PassedChecks.Add("Motion reduction enabled");
        }

        // キーボードナビゲーションをチェック
        var shortcuts = GetKeyboardShortcuts().ToList();
        if (shortcuts.Any())
        {
            result.PassedChecks.Add($"{shortcuts.Count} keyboard shortcuts available");
        }
        else
        {
            result.FailedChecks.Add("No keyboard shortcuts defined");
        }

        result.OverallScore = CalculateAccessibilityScore(result);
        result.Recommendations = GenerateAccessibilityRecommendations(result);

        return result;
    }

    private void InitializeDefaultProfiles()
    {
        // 視覚障害者向けプロファイル
        _profiles["vision-impaired"] = new AccessibilityProfile
        {
            Id = "vision-impaired",
            Name = "Vision Impaired",
            NameJa = "視覚障害者向け",
            Description = "Settings optimized for users with visual impairments",
            DescriptionJa = "視覚障害のあるユーザーに最適化された設定",
            Settings = new AccessibilitySettings
            {
                ScreenReaderEnabled = true,
                HighContrastMode = true,
                TextScaling = 1.5,
                LargeCursor = true,
                SoundFeedback = true,
                ReduceMotion = true,
                AutoFocus = true,
                KeyboardNavigationOnly = false
            }
        };

        // 運動障害者向けプロファイル
        _profiles["motor-impaired"] = new AccessibilityProfile
        {
            Id = "motor-impaired",
            Name = "Motor Impaired",
            NameJa = "運動障害者向け",
            Description = "Settings optimized for users with motor impairments",
            DescriptionJa = "運動障害のあるユーザーに最適化された設定",
            Settings = new AccessibilitySettings
            {
                StickyKeys = true,
                SlowKeys = true,
                MouseKeys = true,
                LargeCursor = true,
                AutoFocus = true,
                KeyboardNavigationOnly = true
            }
        };

        // 認知障害者向けプロファイル
        _profiles["cognitive-impaired"] = new AccessibilityProfile
        {
            Id = "cognitive-impaired",
            Name = "Cognitive Impaired",
            NameJa = "認知障害者向け",
            Description = "Settings optimized for users with cognitive impairments",
            DescriptionJa = "認知障害のあるユーザーに最適化された設定",
            Settings = new AccessibilitySettings
            {
                ReduceMotion = true,
                SoundFeedback = true,
                SimpleLanguage = true,
                VisualCues = true,
                AutoSave = true
            }
        };

        // デフォルトプロファイル
        _profiles["default"] = new AccessibilityProfile
        {
            Id = "default",
            Name = "Default",
            NameJa = "デフォルト",
            Description = "Standard accessibility settings",
            DescriptionJa = "標準的なアクセシビリティ設定",
            Settings = new AccessibilitySettings
            {
                ScreenReaderEnabled = false,
                HighContrastMode = false,
                TextScaling = 1.0,
                ReduceMotion = false
            }
        };
    }

    private void LoadSettings()
    {
        // 実際の実装では設定ファイルを読み込む
        _currentSettings = _profiles["default"].Settings;
    }

    private async Task SaveSettingsAsync()
    {
        // 実際の実装では設定ファイルを保存
        await Task.CompletedTask;
    }

    private async Task OnSettingsChangedAsync()
    {
        // 設定変更イベントを通知
        SettingsChanged?.Invoke(this, EventArgs.Empty);
        await Task.CompletedTask;
    }

    public event EventHandler? SettingsChanged;

    private static double CalculateContrastRatio(System.Drawing.Color color1, System.Drawing.Color color2)
    {
        // 相対輝度を計算
        double lum1 = GetRelativeLuminance(color1);
        double lum2 = GetRelativeLuminance(color2);

        double lighter = Math.Max(lum1, lum2);
        double darker = Math.Min(lum1, lum2);

        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double GetRelativeLuminance(System.Drawing.Color color)
    {
        double r = color.R / 255.0;
        double g = color.G / 255.0;
        double b = color.B / 255.0;

        // ガンマ補正
        r = r <= 0.03928 ? r / 12.92 : Math.Pow((r + 0.055) / 1.055, 2.4);
        g = g <= 0.03928 ? g / 12.92 : Math.Pow((g + 0.055) / 1.055, 2.4);
        b = b <= 0.03928 ? b / 12.92 : Math.Pow((b + 0.055) / 1.055, 2.4);

        return 0.2126 * r + 0.7152 * g + 0.0722 * b;
    }

    private static double CalculateAccessibilityScore(AccessibilityAuditResult result)
    {
        var totalChecks = result.PassedChecks.Count + result.FailedChecks.Count;
        if (totalChecks == 0) return 0;

        return (double)result.PassedChecks.Count / totalChecks * 100;
    }

    private static List<string> GenerateAccessibilityRecommendations(AccessibilityAuditResult result)
    {
        var recommendations = new List<string>();

        if (result.FailedChecks.Any())
        {
            recommendations.Add("Enable high contrast mode for better visibility");
            recommendations.Add("Increase text scaling for improved readability");
            recommendations.Add("Enable screen reader support if needed");
            recommendations.Add("Configure keyboard shortcuts for efficient navigation");
        }

        if (result.OverallScore < 70)
        {
            recommendations.Add("Consider using one of the predefined accessibility profiles");
            recommendations.Add("Consult with accessibility experts for comprehensive evaluation");
        }

        return recommendations;
    }

    // データモデル
    public class AccessibilitySettings
    {
        public bool ScreenReaderEnabled { get; set; }
        public bool HighContrastMode { get; set; }
        public double TextScaling { get; set; } = 1.0;
        public bool LargeCursor { get; set; }
        public bool SoundFeedback { get; set; }
        public bool ReduceMotion { get; set; }
        public bool StickyKeys { get; set; }
        public bool SlowKeys { get; set; }
        public bool MouseKeys { get; set; }
        public bool AutoFocus { get; set; }
        public bool KeyboardNavigationOnly { get; set; }
        public bool SimpleLanguage { get; set; }
        public bool VisualCues { get; set; }
        public bool AutoSave { get; set; }
        public HashSet<string> DisabledShortcuts { get; set; } = new();
    }

    public class AccessibilityProfile
    {
        public string Id = "";
        public string Name = "";
        public string NameJa = "";
        public string Description = "";
        public string DescriptionJa = "";
        public AccessibilitySettings Settings = new();
    }

    public class KeyboardShortcut
    {
        public string Key = "";
        public ModifierKeys Modifiers;
        public string Action = "";
        public string Description = "";
        public string DescriptionJa = "";
    }

    [Flags]
    public enum ModifierKeys
    {
        None = 0,
        Control = 1,
        Alt = 2,
        Shift = 4,
        Windows = 8
    }

    public class ScreenReaderText
    {
        public string ElementId = "";
        public string PrimaryText = "";
        public string SecondaryText = "";
        public string AdditionalInfo = "";
        public string Context = "";
        public string AccessKey = "";
    }

    public class FocusNavigation
    {
        public string CurrentElement = "";
        public NavigationDirection Direction;
        public string NextElement = "";
        public bool IsValid;
    }

    public enum NavigationDirection
    {
        Next,
        Previous,
        First,
        Last
    }

    public class ColorContrastValidation
    {
        public System.Drawing.Color ForegroundColor;
        public System.Drawing.Color BackgroundColor;
        public double ContrastRatio;
        public bool AANormalText;
        public bool AALargeText;
        public bool AAANormalText;
        public bool AAALargeText;
    }

    public class AccessibilityAuditResult
    {
        public DateTime AuditTime;
        public double OverallScore;
        public List<string> PassedChecks = new();
        public List<string> FailedChecks = new();
        public List<string> Recommendations = new();
    }
}
