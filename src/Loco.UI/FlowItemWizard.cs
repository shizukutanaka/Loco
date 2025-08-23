using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Ookii.Dialogs.Wpf;

namespace Loco.UI.FlowBuilder;

/// <summary>
/// Wizard for configuring flow items
/// Step-by-step configuration with visual feedback
/// </summary>
public class FlowItemWizard : Window
{
    private readonly ToolboxItem _item;
    private readonly Dictionary<string, object> _configuration;
    private readonly StackPanel _contentPanel;
    private readonly Button _backButton;
    private readonly Button _nextButton;
    private readonly Button _finishButton;
    private readonly System.Windows.Controls.ProgressBar _progressBar;
    private readonly TextBlock _stepLabel;
    
    private int _currentStep = 0;
    private readonly List<WizardStep> _steps = new();
    
    public FlowItemWizard(ToolboxItem item, Dictionary<string, object> existingConfig = null)
    {
        _item = item;
        _configuration = existingConfig ?? new Dictionary<string, object>();
        
        Title = $"{item.Name} - 設定ウィザード";
        Width = 600;
        Height = 500;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new SolidColorBrush(Color.FromRgb(45, 45, 48));
        Foreground = Brushes.White;
        
        InitializeUI();
        LoadSteps();
        ShowCurrentStep();
    }
    
    public Dictionary<string, object> Configuration => _configuration;
    
    private void InitializeUI()
    {
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Progress
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Content
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Buttons
        
        // Header
        var header = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0, 122, 204)),
            Padding = new Thickness(20)
        };
        
        var headerContent = new StackPanel();
        headerContent.Children.Add(new TextBlock
        {
            Text = $"{_item.Icon} {_item.Name}",
            FontSize = 24,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.White
        });
        headerContent.Children.Add(new TextBlock
        {
            Text = _item.Description,
            FontSize = 14,
            Foreground = Brushes.LightGray,
            Margin = new Thickness(0, 5, 0, 0)
        });
        
        header.Child = headerContent;
        Grid.SetRow(header, 0);
        grid.Children.Add(header);
        
        // Progress
        var progressPanel = new StackPanel { Margin = new Thickness(20, 10, 20, 10) };
        _stepLabel = new TextBlock
        {
            Text = "ステップ 1 / 1",
            Foreground = Brushes.LightGray,
            Margin = new Thickness(0, 0, 0, 5)
        };
        progressPanel.Children.Add(_stepLabel);
        
        _progressBar = new System.Windows.Controls.ProgressBar
        {
            Height = 6,
            Background = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
            Foreground = new SolidColorBrush(Color.FromRgb(0, 122, 204))
        };
        progressPanel.Children.Add(_progressBar);
        
        Grid.SetRow(progressPanel, 1);
        grid.Children.Add(progressPanel);
        
        // Content
        _contentPanel = new StackPanel
        {
            Margin = new Thickness(20)
        };
        
        var contentScroll = new ScrollViewer
        {
            Content = _contentPanel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        
        Grid.SetRow(contentScroll, 2);
        grid.Children.Add(contentScroll);
        
        // Buttons
        var buttonPanel = new Grid
        {
            Background = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
            Height = 60
        };
        
        var buttonStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(20)
        };
        
        _backButton = CreateButton("戻る", false);
        _backButton.Click += BackButton_Click;
        buttonStack.Children.Add(_backButton);
        
        _nextButton = CreateButton("次へ", true);
        _nextButton.Click += NextButton_Click;
        buttonStack.Children.Add(_nextButton);
        
        _finishButton = CreateButton("完了", true);
        _finishButton.Click += FinishButton_Click;
        _finishButton.Visibility = Visibility.Collapsed;
        buttonStack.Children.Add(_finishButton);
        
        var cancelButton = CreateButton("キャンセル", false);
        cancelButton.Click += (s, e) => DialogResult = false;
        buttonStack.Children.Add(cancelButton);
        
        buttonPanel.Children.Add(buttonStack);
        Grid.SetRow(buttonPanel, 3);
        grid.Children.Add(buttonPanel);
        
        Content = grid;
    }
    
    private Button CreateButton(string text, bool isPrimary)
    {
        return new Button
        {
            Content = text,
            Padding = new Thickness(20, 8, 20, 8),
            Margin = new Thickness(5, 0, 5, 0),
            Background = isPrimary 
                ? new SolidColorBrush(Color.FromRgb(0, 122, 204))
                : new SolidColorBrush(Color.FromRgb(80, 80, 80)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            FontWeight = isPrimary ? FontWeights.Bold : FontWeights.Normal
        };
    }
    
    private void LoadSteps()
    {
        _steps.Clear();
        
        switch (_item.Type)
        {
            case "time":
                LoadTimeSteps();
                break;
            case "fileSystem":
                LoadFileSystemSteps();
                break;
            case "httpRequest":
                LoadHttpRequestSteps();
                break;
            case "notification":
                LoadNotificationSteps();
                break;
            case "llmQuery":
                LoadLlmQuerySteps();
                break;
            case "application":
                LoadApplicationSteps();
                break;
            default:
                LoadDefaultSteps();
                break;
        }
    }
    
    private void LoadTimeSteps()
    {
        _steps.Add(new WizardStep
        {
            Title = "実行タイミング",
            Controls = new List<UIElement>
            {
                CreateRadioGroup("triggerType", new[]
                {
                    ("specific", "⏰ 特定の時刻", "毎日決まった時刻に実行"),
                    ("interval", "🔁 一定間隔", "指定した間隔で繰り返し実行"),
                    ("cron", "📅 高度な設定", "Cron式で詳細な設定")
                })
            }
        });
        
        _steps.Add(new WizardStep
        {
            Title = "時刻設定",
            Condition = () => _configuration.GetValueOrDefault("triggerType")?.ToString() == "specific",
            Controls = new List<UIElement>
            {
                CreateTimePicker("time", "実行時刻"),
                CreateCheckBoxList("days", "実行する曜日", new[]
                {
                    ("monday", "月曜日"),
                    ("tuesday", "火曜日"),
                    ("wednesday", "水曜日"),
                    ("thursday", "木曜日"),
                    ("friday", "金曜日"),
                    ("saturday", "土曜日"),
                    ("sunday", "日曜日")
                })
            }
        });
        
        _steps.Add(new WizardStep
        {
            Title = "間隔設定",
            Condition = () => _configuration.GetValueOrDefault("triggerType")?.ToString() == "interval",
            Controls = new List<UIElement>
            {
                CreateNumberInput("interval", "実行間隔", "分", 1, 1440),
                CreateCheckBox("randomDelay", "ランダムな遅延を追加（±30秒）")
            }
        });
    }
    
    private void LoadFileSystemSteps()
    {
        _steps.Add(new WizardStep
        {
            Title = "監視対象",
            Controls = new List<UIElement>
            {
                CreateFolderPicker("path", "監視するフォルダ"),
                CreateTextInput("filter", "ファイルフィルタ", "*.*"),
                CreateCheckBox("includeSubdirectories", "サブフォルダも監視")
            }
        });
        
        _steps.Add(new WizardStep
        {
            Title = "監視イベント",
            Controls = new List<UIElement>
            {
                CreateCheckBoxList("events", "検知するイベント", new[]
                {
                    ("created", "ファイル作成"),
                    ("modified", "ファイル変更"),
                    ("deleted", "ファイル削除"),
                    ("renamed", "ファイル名変更")
                })
            }
        });
    }
    
    private void LoadHttpRequestSteps()
    {
        _steps.Add(new WizardStep
        {
            Title = "リクエスト設定",
            Controls = new List<UIElement>
            {
                CreateTextInput("url", "URL", "https://"),
                CreateDropdown("method", "メソッド", new[]
                {
                    ("GET", "GET - データ取得"),
                    ("POST", "POST - データ送信"),
                    ("PUT", "PUT - データ更新"),
                    ("DELETE", "DELETE - データ削除")
                })
            }
        });
        
        _steps.Add(new WizardStep
        {
            Title = "詳細設定",
            Controls = new List<UIElement>
            {
                CreateMultiLineTextInput("headers", "ヘッダー（JSON形式）", "{}"),
                CreateMultiLineTextInput("body", "ボディ（POSTの場合）", ""),
                CreateNumberInput("timeout", "タイムアウト", "秒", 1, 300, 30)
            }
        });
    }
    
    private void LoadNotificationSteps()
    {
        _steps.Add(new WizardStep
        {
            Title = "通知内容",
            Controls = new List<UIElement>
            {
                CreateTextInput("title", "タイトル", "Loco 通知"),
                CreateMultiLineTextInput("message", "メッセージ", ""),
                CreateDropdown("priority", "優先度", new[]
                {
                    ("low", "低"),
                    ("normal", "通常"),
                    ("high", "高"),
                    ("urgent", "緊急")
                })
            }
        });
        
        _steps.Add(new WizardStep
        {
            Title = "表示設定",
            Controls = new List<UIElement>
            {
                CreateCheckBox("playSound", "サウンドを再生"),
                CreateNumberInput("duration", "表示時間", "秒", 1, 60, 5),
                CreateIconPicker("icon", "アイコン")
            }
        });
    }
    
    private void LoadLlmQuerySteps()
    {
        _steps.Add(new WizardStep
        {
            Title = "LLM設定",
            Controls = new List<UIElement>
            {
                CreateDropdown("model", "使用モデル", new[]
                {
                    ("local-small", "ローカル小型モデル（高速）"),
                    ("local-medium", "ローカル中型モデル（バランス）"),
                    ("local-large", "ローカル大型モデル（高精度）"),
                    ("cloud", "クラウドモデル（最高精度）")
                })
            }
        });
        
        _steps.Add(new WizardStep
        {
            Title = "プロンプト設定",
            Controls = new List<UIElement>
            {
                CreateMultiLineTextInput("prompt", "プロンプト", ""),
                CreateVariableSelector("variables", "使用する変数"),
                CreateNumberInput("maxTokens", "最大トークン数", "", 1, 4000, 500),
                CreateSlider("temperature", "創造性", 0, 1, 0.7)
            }
        });
    }
    
    private void LoadApplicationSteps()
    {
        _steps.Add(new WizardStep
        {
            Title = "アプリケーション選択",
            Controls = new List<UIElement>
            {
                CreateAppPicker("appPath", "アプリケーション"),
                CreateTextInput("arguments", "起動引数", ""),
                CreateFolderPicker("workingDirectory", "作業フォルダ（オプション）")
            }
        });
        
        _steps.Add(new WizardStep
        {
            Title = "実行設定",
            Controls = new List<UIElement>
            {
                CreateDropdown("windowStyle", "ウィンドウ表示", new[]
                {
                    ("normal", "通常"),
                    ("minimized", "最小化"),
                    ("maximized", "最大化"),
                    ("hidden", "非表示")
                }),
                CreateCheckBox("waitForExit", "終了まで待機"),
                CreateCheckBox("runAsAdmin", "管理者として実行")
            }
        });
    }
    
    private void LoadDefaultSteps()
    {
        _steps.Add(new WizardStep
        {
            Title = "基本設定",
            Controls = new List<UIElement>
            {
                CreateTextInput("name", "名前", _item.Name),
                CreateMultiLineTextInput("description", "説明", "")
            }
        });
    }
    
    // UI Control creation methods
    private UIElement CreateRadioGroup(string key, (string value, string label, string description)[] options)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 10, 0, 10) };
        
        foreach (var option in options)
        {
            var radioPanel = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                CornerRadius = new CornerRadius(5),
                Margin = new Thickness(0, 5, 0, 5),
                Padding = new Thickness(15)
            };
            
            var radio = new RadioButton
            {
                GroupName = key,
                Tag = option.value,
                IsChecked = _configuration.GetValueOrDefault(key)?.ToString() == option.value
            };
            
            radio.Checked += (s, e) => _configuration[key] = option.value;
            
            var content = new StackPanel { Orientation = Orientation.Horizontal };
            content.Children.Add(new TextBlock
            {
                Text = option.label,
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Margin = new Thickness(5, 0, 10, 0)
            });
            content.Children.Add(new TextBlock
            {
                Text = option.description,
                FontSize = 12,
                Foreground = Brushes.LightGray,
                VerticalAlignment = VerticalAlignment.Center
            });
            
            radio.Content = content;
            radioPanel.Child = radio;
            panel.Children.Add(radioPanel);
        }
        
        return panel;
    }
    
    private UIElement CreateTextInput(string key, string label, string defaultValue = "")
    {
        var panel = new StackPanel { Margin = new Thickness(0, 10, 0, 10) };
        
        panel.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = Brushes.LightGray,
            Margin = new Thickness(0, 0, 0, 5)
        });
        
        var textBox = new TextBox
        {
            Text = _configuration.GetValueOrDefault(key)?.ToString() ?? defaultValue,
            Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
            Padding = new Thickness(10),
            FontSize = 14
        };
        
        textBox.TextChanged += (s, e) => _configuration[key] = textBox.Text;
        panel.Children.Add(textBox);
        
        return panel;
    }
    
    private UIElement CreateMultiLineTextInput(string key, string label, string defaultValue = "")
    {
        var panel = new StackPanel { Margin = new Thickness(0, 10, 0, 10) };
        
        panel.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = Brushes.LightGray,
            Margin = new Thickness(0, 0, 0, 5)
        });
        
        var textBox = new TextBox
        {
            Text = _configuration.GetValueOrDefault(key)?.ToString() ?? defaultValue,
            Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
            Padding = new Thickness(10),
            FontSize = 14,
            MinHeight = 100,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        
        textBox.TextChanged += (s, e) => _configuration[key] = textBox.Text;
        panel.Children.Add(textBox);
        
        return panel;
    }
    
    private UIElement CreateNumberInput(string key, string label, string unit, int min, int max, int defaultValue = 0)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 10, 0, 10) };
        
        panel.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = Brushes.LightGray,
            Margin = new Thickness(0, 0, 0, 5)
        });
        
        var inputPanel = new Grid();
        inputPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        inputPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        
        var slider = new Slider
        {
            Minimum = min,
            Maximum = max,
            Value = Convert.ToDouble(_configuration.GetValueOrDefault(key) ?? defaultValue),
            TickFrequency = (max - min) / 10.0,
            IsSnapToTickEnabled = true
        };
        
        var valueLabel = new TextBlock
        {
            Text = $"{slider.Value} {unit}",
            Foreground = Brushes.White,
            Margin = new Thickness(10, 0, 0, 0),
            MinWidth = 60
        };
        
        slider.ValueChanged += (s, e) =>
        {
            _configuration[key] = (int)slider.Value;
            valueLabel.Text = $"{(int)slider.Value} {unit}";
        };
        
        Grid.SetColumn(slider, 0);
        Grid.SetColumn(valueLabel, 1);
        inputPanel.Children.Add(slider);
        inputPanel.Children.Add(valueLabel);
        
        panel.Children.Add(inputPanel);
        
        return panel;
    }
    
    private UIElement CreateCheckBox(string key, string label)
    {
        var checkBox = new CheckBox
        {
            Content = label,
            Foreground = Brushes.White,
            IsChecked = Convert.ToBoolean(_configuration.GetValueOrDefault(key) ?? false),
            Margin = new Thickness(0, 10, 0, 10)
        };
        
        checkBox.Checked += (s, e) => _configuration[key] = true;
        checkBox.Unchecked += (s, e) => _configuration[key] = false;
        
        return checkBox;
    }
    
    private UIElement CreateCheckBoxList(string key, string label, (string value, string text)[] options)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 10, 0, 10) };
        
        panel.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = Brushes.LightGray,
            Margin = new Thickness(0, 0, 0, 5)
        });
        
        var selectedValues = (_configuration.GetValueOrDefault(key) as List<string>) ?? new List<string>();
        
        foreach (var option in options)
        {
            var checkBox = new CheckBox
            {
                Content = option.text,
                Tag = option.value,
                Foreground = Brushes.White,
                IsChecked = selectedValues.Contains(option.value),
                Margin = new Thickness(20, 5, 0, 5)
            };
            
            checkBox.Checked += (s, e) =>
            {
                if (!selectedValues.Contains(option.value))
                    selectedValues.Add(option.value);
                _configuration[key] = selectedValues;
            };
            
            checkBox.Unchecked += (s, e) =>
            {
                selectedValues.Remove(option.value);
                _configuration[key] = selectedValues;
            };
            
            panel.Children.Add(checkBox);
        }
        
        return panel;
    }
    
    private UIElement CreateDropdown(string key, string label, (string value, string text)[] options)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 10, 0, 10) };
        
        panel.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = Brushes.LightGray,
            Margin = new Thickness(0, 0, 0, 5)
        });
        
        var comboBox = new ComboBox
        {
            Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(80, 80, 80))
        };
        
        foreach (var option in options)
        {
            var item = new ComboBoxItem
            {
                Content = option.text,
                Tag = option.value
            };
            comboBox.Items.Add(item);
            
            if (_configuration.GetValueOrDefault(key)?.ToString() == option.value)
                comboBox.SelectedItem = item;
        }
        
        comboBox.SelectionChanged += (s, e) =>
        {
            if (comboBox.SelectedItem is ComboBoxItem item)
                _configuration[key] = item.Tag;
        };
        
        panel.Children.Add(comboBox);
        
        return panel;
    }
    
    private UIElement CreateTimePicker(string key, string label)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 10, 0, 10) };
        
        panel.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = Brushes.LightGray,
            Margin = new Thickness(0, 0, 0, 5)
        });
        
        var timePanel = new StackPanel { Orientation = Orientation.Horizontal };
        
        var hourCombo = new ComboBox
        {
            Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
            Foreground = Brushes.White,
            Width = 60
        };
        
        for (int i = 0; i < 24; i++)
            hourCombo.Items.Add(i.ToString("00"));
        
        var minuteCombo = new ComboBox
        {
            Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
            Foreground = Brushes.White,
            Width = 60,
            Margin = new Thickness(10, 0, 0, 0)
        };
        
        for (int i = 0; i < 60; i += 5)
            minuteCombo.Items.Add(i.ToString("00"));
        
        var currentTime = _configuration.GetValueOrDefault(key)?.ToString() ?? "00:00";
        var parts = currentTime.Split(':');
        
        hourCombo.SelectedItem = parts.Length > 0 ? parts[0] : "00";
        minuteCombo.SelectedItem = parts.Length > 1 ? parts[1] : "00";
        
        Action updateTime = () =>
        {
            _configuration[key] = $"{hourCombo.SelectedItem}:{minuteCombo.SelectedItem}";
        };
        
        hourCombo.SelectionChanged += (s, e) => updateTime();
        minuteCombo.SelectionChanged += (s, e) => updateTime();
        
        timePanel.Children.Add(hourCombo);
        timePanel.Children.Add(new TextBlock
        {
            Text = ":",
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(5, 0, 5, 0)
        });
        timePanel.Children.Add(minuteCombo);
        
        panel.Children.Add(timePanel);
        
        return panel;
    }
    
    private UIElement CreateFolderPicker(string key, string label)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 10, 0, 10) };
        
        panel.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = Brushes.LightGray,
            Margin = new Thickness(0, 0, 0, 5)
        });
        
        var inputPanel = new Grid();
        inputPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        inputPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        
        var textBox = new TextBox
        {
            Text = _configuration.GetValueOrDefault(key)?.ToString() ?? "",
            Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
            Padding = new Thickness(10)
        };
        
        var browseButton = new Button
        {
            Content = "参照...",
            Margin = new Thickness(5, 0, 0, 0),
            Padding = new Thickness(15, 5, 15, 5),
            Background = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0)
        };
        
        browseButton.Click += (s, e) =>
        {
            var dialog = new VistaFolderBrowserDialog
            {
                Description = "フォルダーを選択",
                ShowNewFolderButton = true
            };
            var result = dialog.ShowDialog(this);
            if (result == true)
            {
                textBox.Text = dialog.SelectedPath;
                _configuration[key] = dialog.SelectedPath;
            }
        };
        
        textBox.TextChanged += (s, e) => _configuration[key] = textBox.Text;
        
        Grid.SetColumn(textBox, 0);
        Grid.SetColumn(browseButton, 1);
        inputPanel.Children.Add(textBox);
        inputPanel.Children.Add(browseButton);
        
        panel.Children.Add(inputPanel);
        
        return panel;
    }
    
    private UIElement CreateSlider(string key, string label, double min, double max, double defaultValue)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 10, 0, 10) };
        
        panel.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = Brushes.LightGray,
            Margin = new Thickness(0, 0, 0, 5)
        });
        
        var slider = new Slider
        {
            Minimum = min,
            Maximum = max,
            Value = Convert.ToDouble(_configuration.GetValueOrDefault(key) ?? defaultValue),
            TickFrequency = (max - min) / 10.0,
            IsSnapToTickEnabled = false
        };
        
        var valueLabel = new TextBlock
        {
            Text = slider.Value.ToString("F2"),
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 5, 0, 0)
        };
        
        slider.ValueChanged += (s, e) =>
        {
            _configuration[key] = slider.Value;
            valueLabel.Text = slider.Value.ToString("F2");
        };
        
        panel.Children.Add(slider);
        panel.Children.Add(valueLabel);
        
        return panel;
    }
    
    private UIElement CreateAppPicker(string key, string label)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 10, 0, 10) };
        
        panel.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = Brushes.LightGray,
            Margin = new Thickness(0, 0, 0, 5)
        });
        
        // Quick app buttons
        var quickApps = new WrapPanel { Margin = new Thickness(0, 5, 0, 10) };
        
        var apps = new[]
        {
            ("notepad.exe", "メモ帳", "📝"),
            ("calc.exe", "電卓", "🔢"),
            ("mspaint.exe", "ペイント", "🎨"),
            ("explorer.exe", "エクスプローラー", "📁")
        };
        
        foreach (var app in apps)
        {
            var button = new Button
            {
                Content = new StackPanel
                {
                    Children =
                    {
                        new TextBlock { Text = app.Item3, FontSize = 20 },
                        new TextBlock { Text = app.Item2, FontSize = 10 }
                    }
                },
                Width = 80,
                Height = 60,
                Margin = new Thickness(5),
                Background = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0)
            };
            
            button.Click += (s, e) => _configuration[key] = app.Item1;
            quickApps.Children.Add(button);
        }
        
        panel.Children.Add(quickApps);
        
        // Custom path input
        var customPanel = CreateTextInput(key, "カスタムパス", "");
        panel.Children.Add(customPanel);
        
        return panel;
    }
    
    private UIElement CreateIconPicker(string key, string label)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 10, 0, 10) };
        
        panel.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = Brushes.LightGray,
            Margin = new Thickness(0, 0, 0, 5)
        });
        
        var iconPanel = new WrapPanel();
        
        var icons = new[] { "ℹ️", "⚠️", "❌", "✅", "🔔", "📧", "💬", "🎯", "⭐", "🔥" };
        
        foreach (var icon in icons)
        {
            var button = new RadioButton
            {
                Content = new TextBlock { Text = icon, FontSize = 24 },
                GroupName = key,
                Margin = new Thickness(5),
                IsChecked = _configuration.GetValueOrDefault(key)?.ToString() == icon
            };
            
            button.Checked += (s, e) => _configuration[key] = icon;
            iconPanel.Children.Add(button);
        }
        
        panel.Children.Add(iconPanel);
        
        return panel;
    }
    
    private UIElement CreateVariableSelector(string key, string label)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 10, 0, 10) };
        
        panel.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = Brushes.LightGray,
            Margin = new Thickness(0, 0, 0, 5)
        });
        
        var variablePanel = new WrapPanel();
        
        var variables = new[]
        {
            ("${timestamp}", "タイムスタンプ"),
            ("${date}", "日付"),
            ("${time}", "時刻"),
            ("${filename}", "ファイル名"),
            ("${filepath}", "ファイルパス"),
            ("${response}", "前の応答")
        };
        
        foreach (var variable in variables)
        {
            var button = new Button
            {
                Content = new StackPanel
                {
                    Children =
                    {
                        new TextBlock { Text = variable.Item1, FontFamily = new FontFamily("Consolas") },
                        new TextBlock { Text = variable.Item2, FontSize = 10, Foreground = Brushes.Gray }
                    }
                },
                Margin = new Thickness(5),
                Padding = new Thickness(10, 5, 10, 5),
                Background = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0)
            };
            
            button.Click += (s, e) =>
            {
                // Insert variable into prompt text if exists
                if (_configuration.TryGetValue("prompt", out var prompt))
                {
                    _configuration["prompt"] = prompt + " " + variable.Item1;
                }
            };
            
            variablePanel.Children.Add(button);
        }
        
        panel.Children.Add(variablePanel);
        
        return panel;
    }
    
    private void ShowCurrentStep()
    {
        _contentPanel.Children.Clear();
        
        if (_currentStep < 0 || _currentStep >= _steps.Count)
            return;
        
        var step = _steps[_currentStep];
        
        // Skip steps with false conditions
        while (step.Condition != null && !step.Condition())
        {
            _currentStep++;
            if (_currentStep >= _steps.Count)
            {
                FinishButton_Click(null, null);
                return;
            }
            step = _steps[_currentStep];
        }
        
        // Update progress
        _stepLabel.Text = $"ステップ {_currentStep + 1} / {_steps.Count}";
        _progressBar.Value = (double)(_currentStep + 1) / _steps.Count * 100;
        
        // Add step title
        _contentPanel.Children.Add(new TextBlock
        {
            Text = step.Title,
            FontSize = 20,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 20)
        });
        
        // Add step controls
        foreach (var control in step.Controls)
        {
            _contentPanel.Children.Add(control);
        }
        
        // Update button visibility
        _backButton.IsEnabled = _currentStep > 0;
        _nextButton.Visibility = _currentStep < _steps.Count - 1 ? Visibility.Visible : Visibility.Collapsed;
        _finishButton.Visibility = _currentStep == _steps.Count - 1 ? Visibility.Visible : Visibility.Collapsed;
    }
    
    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentStep > 0)
        {
            _currentStep--;
            ShowCurrentStep();
        }
    }
    
    private void NextButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentStep < _steps.Count - 1)
        {
            _currentStep++;
            ShowCurrentStep();
        }
    }
    
    private void FinishButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
    
    private class WizardStep
    {
        public string Title { get; set; }
        public List<UIElement> Controls { get; set; }
        public Func<bool> Condition { get; set; }
    }
}