using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.ComponentModel;
using System.Windows.Data;
using Loco.Core.FlowComposer;
using Loco.Core.Models;
using AutomationDsl = Loco.Core.Models.AutomationDsl;
using Loco.UI.Controls;
using Loco.UI.Commands;
using Microsoft.Extensions.Logging;
using Loco.Automation.Interfaces;
using Microsoft.Win32;
using FlowBuilderType = Loco.Core.FlowComposer.FlowBuilder;
using Loco.UI.Themes;

namespace Loco.UI;

public partial class MainWindow : Window
{
    private readonly FlowComposerBuilder _flowComposerBuilder;
    private readonly ILogger<MainWindow> _logger;
    private readonly INaturalLanguageRuleService _nlService;
    private readonly IAutomationService _automationService;
    private readonly Services.IValidationService _validationService;
    private readonly Services.SettingsService _settingsService;
    private readonly LlmModelManager _modelManager;
    private FlowBuilderType _currentFlowBuilder;
    private readonly ObservableCollection<FlowListItem> _flows;
    private readonly List<FlowComponent> _currentFlowComponents;
    private readonly CommandManager _commandManager;
    private readonly ICollectionView _flowsView;
    
    // Expose flows for XAML binding (Saved Flows ListView)
    public ObservableCollection<FlowListItem> Flows => _flows;

    public MainWindow(ILogger<MainWindow> logger,
                      INaturalLanguageRuleService nlService,
                      IAutomationService automationService,
                      Services.IValidationService validationService,
                      Services.SettingsService settingsService,
                      LlmModelManager modelManager)
    {
        InitializeComponent();
        
        // Injected services
        _logger = logger;
        _nlService = nlService;
        _automationService = automationService;
        _validationService = validationService;
        _settingsService = settingsService;
        _modelManager = modelManager;
        
        // Initialize Flow Composer builder
        _flowComposerBuilder = new FlowComposerBuilder(_logger);
        
        // Initialize collections
        _flows = new ObservableCollection<FlowListItem>();
        _currentFlowComponents = new List<FlowComponent>();
        _commandManager = new CommandManager();
        
        // Create a view for sorting and filtering Saved Flows
        _flowsView = CollectionViewSource.GetDefaultView(_flows);
        if (_flowsView != null)
        {
            _flowsView.SortDescriptions.Clear();
            _flowsView.SortDescriptions.Add(new SortDescription(nameof(FlowListItem.CreatedAt), ListSortDirection.Descending));
            _flowsView.Filter = FlowFilter;
        }

    /// <summary>
    /// Handle model registry changes by refreshing the ModelSelector while preserving selection
    /// </summary>
    private void OnModelsChanged(object sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            if (ModelSelector == null) return;
            
            // Remember current selection and saved preference
            var prevSelected = (ModelSelector.SelectedItem as ComboBoxItem)?.Content?.ToString();
            var savedName = _settingsService.SelectedModel;
            var savedId = _settingsService.SelectedModelId;
            
            // Repopulate list
            PopulateModelSelector();
            
            // 1) Prefer saved model by ID
            var matchedById = false;
            if (!string.IsNullOrWhiteSpace(savedId))
            {
                foreach (ComboBoxItem item in ModelSelector.Items)
                {
                    if (string.Equals(item.Tag?.ToString(), savedId, StringComparison.OrdinalIgnoreCase))
                    {
                        ModelSelector.SelectedItem = item;
                        // Persist to keep name/id synchronized
                        _settingsService.SelectedModel = item.Content?.ToString();
                        _settingsService.SelectedModelId = savedId;
                        matchedById = true;
                        break;
                    }
                }
            }

            // 2) Fallback: match by saved name or previous selection
            if (!matchedById)
            {
                var desired = !string.IsNullOrWhiteSpace(savedName) ? savedName : prevSelected;
                if (!string.IsNullOrWhiteSpace(desired))
                {
                    foreach (ComboBoxItem item in ModelSelector.Items)
                    {
                        if (string.Equals(item.Content?.ToString(), desired, StringComparison.OrdinalIgnoreCase))
                        {
                            ModelSelector.SelectedItem = item;
                            // Persist to migrate to stable ID when available
                            _settingsService.SelectedModel = item.Content?.ToString();
                            var tag2 = item.Tag?.ToString();
                            _settingsService.SelectedModelId = (!string.Equals(tag2, "custom", StringComparison.OrdinalIgnoreCase) &&
                                                               !string.Equals(tag2, "placeholder", StringComparison.OrdinalIgnoreCase))
                                                               ? tag2 : string.Empty;
                            break;
                        }
                    }
                }
            }

            // Fallback: if still nothing selected, choose first non-custom item and persist
            if (ModelSelector.SelectedItem == null && ModelSelector.Items.Count > 0)
            {
                foreach (ComboBoxItem item in ModelSelector.Items)
                {
                    var tag = item.Tag?.ToString();
                    if (!string.Equals(tag, "custom", StringComparison.OrdinalIgnoreCase))
                    {
                        ModelSelector.SelectedItem = item;
                        _settingsService.SelectedModel = item.Content?.ToString();
                        _settingsService.SelectedModelId = (!string.Equals(tag, "custom", StringComparison.OrdinalIgnoreCase) &&
                                                           !string.Equals(tag, "placeholder", StringComparison.OrdinalIgnoreCase))
                                                           ? tag : string.Empty;
                        break;
                    }
                }
            }
        });
    }
        
        // Setup UI
        SetupUI();
        LoadSavedFlows();
        
        // Keep model selector in sync with registry changes
        _modelManager.ModelsChanged += OnModelsChanged;
        
        // Register keyboard shortcuts
        RegisterKeyboardShortcuts();
        
        // Register undo/redo keyboard shortcuts
        RegisterUndoRedoShortcuts();

        // Subscribe to CommandManager events
        _commandManager.UndoRedoStateChanged += OnUndoRedoStateChanged;
        UpdateUndoRedoButtons();

        // Subscribe to theme changes to reapply status styling
        ThemeManager.ThemeChanged += OnThemeChanged;
    }
    
    private void SetupUI()
    {
        // Setup component selector events
        if (ComponentSelector != null)
        {
            ComponentSelector.ComponentConfigured += OnComponentConfigured;
        }
        
        // Initialize settings controls
        InitializeSettingsControls();
        
        // Restore Saved Flows expander state and wire events
        if (SavedFlowsExpander != null)
        {
            SavedFlowsExpander.IsExpanded = _settingsService.SavedFlowsExpanded;
            SavedFlowsExpander.Expanded += SavedFlowsExpander_Expanded;
            SavedFlowsExpander.Collapsed += SavedFlowsExpander_Collapsed;
        }
        
        // Restore Saved Flows search text
        if (SavedFlowsSearchBox != null)
        {
            SavedFlowsSearchBox.Text = _settingsService.SavedFlowsSearchText ?? string.Empty;
            _flowsView?.Refresh();
        }
        
        // Set initial status
        UpdateStatus("準備完了", StatusType.Ready);
    }
    
    /// <summary>
    /// Initializes the settings controls with saved values
    /// </summary>
    private void InitializeSettingsControls()
    {
        // Model selector - populate from LlmModelManager and select saved preference
        if (ModelSelector != null)
        {
            PopulateModelSelector();

            // 1) Prefer saved model by ID
            var savedId = _settingsService.SelectedModelId;
            if (!string.IsNullOrWhiteSpace(savedId))
            {
                foreach (ComboBoxItem item in ModelSelector.Items)
                {
                    if (string.Equals(item.Tag?.ToString(), savedId, StringComparison.OrdinalIgnoreCase))
                    {
                        ModelSelector.SelectedItem = item;
                        // Persist to keep name/id synchronized
                        _settingsService.SelectedModel = item.Content?.ToString();
                        _settingsService.SelectedModelId = savedId;
                        break;
                    }
                }
            }

            // 2) Fallback: select saved model by name
            if (ModelSelector.SelectedItem == null)
            {
                var savedModel = _settingsService.SelectedModel;
                if (!string.IsNullOrWhiteSpace(savedModel))
                {
                    foreach (ComboBoxItem item in ModelSelector.Items)
                    {
                        if (string.Equals(item.Content?.ToString(), savedModel, StringComparison.OrdinalIgnoreCase))
                        {
                            ModelSelector.SelectedItem = item;
                            // Persist to migrate to stable ID for legacy name-only settings
                            _settingsService.SelectedModel = item.Content?.ToString();
                            var tag2 = item.Tag?.ToString();
                            _settingsService.SelectedModelId = (!string.Equals(tag2, "custom", StringComparison.OrdinalIgnoreCase) &&
                                                               !string.Equals(tag2, "placeholder", StringComparison.OrdinalIgnoreCase))
                                                               ? tag2 : string.Empty;
                            break;
                        }
                    }
                }
            }

            ModelSelector.SelectionChanged += ModelSelector_SelectionChanged;

            // Fallback: if nothing selected, choose first non-custom item and persist
            if (ModelSelector.SelectedItem == null && ModelSelector.Items.Count > 0)
            {
                foreach (ComboBoxItem item in ModelSelector.Items)
                {
                    var tag = item.Tag?.ToString();
                    if (!string.Equals(tag, "custom", StringComparison.OrdinalIgnoreCase))
                    {
                        ModelSelector.SelectedItem = item;
                        _settingsService.SelectedModel = item.Content?.ToString();
                        _settingsService.SelectedModelId = (!string.Equals(tag, "custom", StringComparison.OrdinalIgnoreCase) &&
                                                           !string.Equals(tag, "placeholder", StringComparison.OrdinalIgnoreCase))
                                                           ? tag : string.Empty;
                        break;
                    }
                }
            }
        }

        // Checkboxes - Load initial values
        AutoStartCheckBox.IsChecked = _settingsService.AutoStartFlows;
        ErrorNotificationCheckBox.IsChecked = _settingsService.ShowErrorNotifications;
        DetailedLogsCheckBox.IsChecked = _settingsService.RecordDetailedLogs;
        SandboxCheckBox.IsChecked = _settingsService.EnableSandboxExecution;
        NetworkRestrictCheckBox.IsChecked = _settingsService.RestrictNetworkAccess;
        FileRestrictCheckBox.IsChecked = _settingsService.RestrictFileAccess;

        // Checkboxes - Add event handlers for auto-saving
        AutoStartCheckBox.Checked += (s, e) => _settingsService.AutoStartFlows = true;
        AutoStartCheckBox.Unchecked += (s, e) => _settingsService.AutoStartFlows = false;

        ErrorNotificationCheckBox.Checked += (s, e) => _settingsService.ShowErrorNotifications = true;
        ErrorNotificationCheckBox.Unchecked += (s, e) => _settingsService.ShowErrorNotifications = false;

        DetailedLogsCheckBox.Checked += (s, e) => _settingsService.RecordDetailedLogs = true;
        DetailedLogsCheckBox.Unchecked += (s, e) => _settingsService.RecordDetailedLogs = false;

        SandboxCheckBox.Checked += (s, e) => _settingsService.EnableSandboxExecution = true;
        SandboxCheckBox.Unchecked += (s, e) => _settingsService.EnableSandboxExecution = false;

        NetworkRestrictCheckBox.Checked += (s, e) => _settingsService.RestrictNetworkAccess = true;
        NetworkRestrictCheckBox.Unchecked += (s, e) => _settingsService.RestrictNetworkAccess = false;

        FileRestrictCheckBox.Checked += (s, e) => _settingsService.RestrictFileAccess = true;
        FileRestrictCheckBox.Unchecked += (s, e) => _settingsService.RestrictFileAccess = false;

        // Theme selector
        if (ThemeSelector != null)
        {
            // Set initial selection from settings
            var desired = _settingsService.Theme;
            foreach (ComboBoxItem item in ThemeSelector.Items)
            {
                var tag = item.Tag?.ToString();
                if (!string.IsNullOrWhiteSpace(tag) && string.Equals(tag, desired, StringComparison.OrdinalIgnoreCase))
                {
                    ThemeSelector.SelectedItem = item;
                    break;
                }
            }

            ThemeSelector.SelectionChanged += ThemeSelector_SelectionChanged;
        }
    }

    /// <summary>
    /// Populate the LLM model selector from the local model registry
    /// </summary>
    private void PopulateModelSelector()
    {
        try
        {
            ModelSelector.Items.Clear();

            // List available models
            var models = _modelManager.GetModels();
            foreach (var m in models)
            {
                var item = new ComboBoxItem
                {
                    Content = m.Name,
                    Tag = m.Id,
                    ToolTip = m.FilePath
                };
                ModelSelector.Items.Add(item);
            }

            // Fallback if no models present
            if (ModelSelector.Items.Count == 0)
            {
                ModelSelector.Items.Add(new ComboBoxItem { Content = "local-model-1 (推奨)", Tag = "placeholder" });
            }

            // Keep a custom entry at the end (non-functional placeholder for now)
            ModelSelector.Items.Add(new ComboBoxItem { Content = "カスタムモデル...", Tag = "custom" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to populate model selector");
        }
    }
    
    /// <summary>
    /// Resolve the currently selected model from settings, preferring ID, with name fallback.
    /// Returns null if not found.
    /// </summary>
    private Loco.Core.Models.ModelInfo GetSelectedModelInfo()
    {
        try
        {
            var id = _settingsService.SelectedModelId;
            if (!string.IsNullOrWhiteSpace(id))
            {
                var byId = _modelManager.GetModel(id);
                if (byId != null) return byId;
            }
            var name = _settingsService.SelectedModel;
            if (!string.IsNullOrWhiteSpace(name))
            {
                var byName = _modelManager.GetModels()
                    .FirstOrDefault(m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase));
                if (byName != null) return byName;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve selected model info");
        }
        return null;
    }
    
    /// <summary>
    /// Handles model selector selection changed event
    /// </summary>
    private void ModelSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ModelSelector.SelectedItem is ComboBoxItem item)
        {
            _settingsService.SelectedModel = item.Content.ToString();
            var tag = item.Tag?.ToString();
            _settingsService.SelectedModelId = (!string.Equals(tag, "custom", StringComparison.OrdinalIgnoreCase) &&
                                               !string.Equals(tag, "placeholder", StringComparison.OrdinalIgnoreCase))
                                               ? tag : string.Empty;
        }
    }

    /// <summary>
    /// Handles theme selection changes: applies theme and persists preference
    /// </summary>
    private void ThemeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ThemeSelector.SelectedItem is ComboBoxItem item)
        {
            var tag = item.Tag?.ToString();
            if (string.IsNullOrWhiteSpace(tag)) return;

            // Persist
            _settingsService.Theme = tag;

            // Apply runtime theme
            ThemeManager.CurrentTheme = string.Equals(tag, nameof(Theme.Light), StringComparison.OrdinalIgnoreCase)
                ? Theme.Light
                : Theme.Dark;
        }
    }

    /// <summary>
    /// Quick toggle from the header button. Keeps selector and settings in sync.
    /// </summary>
    private void ThemeToggle_Click(object sender, RoutedEventArgs e)
    {
        // Toggle current theme
        var newTheme = ThemeManager.CurrentTheme == Theme.Light ? Theme.Dark : Theme.Light;
        ThemeManager.CurrentTheme = newTheme;

        // Persist
        _settingsService.Theme = newTheme.ToString();

        // Sync selector without re-entering logic excessively
        if (ThemeSelector != null)
        {
            foreach (ComboBoxItem item in ThemeSelector.Items)
            {
                var tag = item.Tag?.ToString();
                if (!string.IsNullOrWhiteSpace(tag) &&
                    string.Equals(tag, newTheme.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    ThemeSelector.SelectedItem = item;
                    break;
                }
            }
        }

        // Reapply status style according to theme
        if (!string.IsNullOrEmpty(_lastStatusMessage))
        {
            UpdateStatus(_lastStatusMessage, _lastStatusType);
        }
    }
    
    /// <summary>
    /// Registers keyboard shortcuts for common actions
    /// </summary>
    private void RegisterKeyboardShortcuts()
    {
        // New Flow (Ctrl+N)
        this.InputBindings.Add(new KeyBinding(
            new RelayCommand(_ => NewFlow_Click(null, null)),
            new KeyGesture(Key.N, ModifierKeys.Control)));
            
        // Save Flow (Ctrl+S)
        this.InputBindings.Add(new KeyBinding(
            new RelayCommand(_ => SaveFlow_Click(null, null)),
            new KeyGesture(Key.S, ModifierKeys.Control)));
            
        // Open Flow (Ctrl+O)
        this.InputBindings.Add(new KeyBinding(
            new RelayCommand(_ => OpenFlow_Click(null, null)),
            new KeyGesture(Key.O, ModifierKeys.Control)));
            
        // Delete Flow (Delete)
        this.InputBindings.Add(new KeyBinding(
            new RelayCommand(_ => DeleteFlow_Click(null, null)),
            new KeyGesture(Key.Delete)));
            
        // Run Flow (F5)
        this.InputBindings.Add(new KeyBinding(
            new RelayCommand(_ => RunFlow_Click(null, null)),
            new KeyGesture(Key.F5)));

        // Refresh Saved Flows (Ctrl+R)
        this.InputBindings.Add(new KeyBinding(
            new RelayCommand(_ => LoadSavedFlows()),
            new KeyGesture(Key.R, ModifierKeys.Control)));

        // Edit selected flow (Enter)
        this.InputBindings.Add(new KeyBinding(
            new RelayCommand(_ => EditFlow_Click(null, null)),
            new KeyGesture(Key.Enter)));

        // Duplicate selected flow (Ctrl+D)
        this.InputBindings.Add(new KeyBinding(
            new RelayCommand(_ => DuplicateFlow_Click(null, null)),
            new KeyGesture(Key.D, ModifierKeys.Control)));
    }
    
    /// <summary>
    /// Registers keyboard shortcuts for undo/redo operations
    /// </summary>
    private void RegisterUndoRedoShortcuts()
    {
        // Undo (Ctrl+Z)
        this.InputBindings.Add(new KeyBinding(
            new RelayCommand(_ => Undo_Click(null, null), _ => _commandManager.CanUndo),
            new KeyGesture(Key.Z, ModifierKeys.Control)));

        // Redo (Ctrl+Y)
        this.InputBindings.Add(new KeyBinding(
            new RelayCommand(_ => Redo_Click(null, null), _ => _commandManager.CanRedo),
            new KeyGesture(Key.Y, ModifierKeys.Control)));
    }

    private void OnUndoRedoStateChanged(object sender, EventArgs e)
    {
        UpdateUndoRedoButtons();
    }

    private void UpdateUndoRedoButtons()
    {
        UndoButton.IsEnabled = _commandManager.CanUndo;
        RedoButton.IsEnabled = _commandManager.CanRedo;
    }

    private void Undo_Click(object sender, RoutedEventArgs e)
    {
        _commandManager.Undo();
    }

    private void Redo_Click(object sender, RoutedEventArgs e)
    {
        _commandManager.Redo();
    }
    
    private void LoadSavedFlows()
    {
        try
        {
            // Clear current list to avoid duplicates on reload
            _flows.Clear();

            var flowsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Loco", "Flows");
            
            if (!Directory.Exists(flowsDir))
            {
                Directory.CreateDirectory(flowsDir);
                return;
            }
            
            var flowFiles = Directory.GetFiles(flowsDir, "*.json");
            foreach (var file in flowFiles)
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var flow = JsonSerializer.Deserialize<FlowDefinition>(json);
                    if (flow != null)
                    {
                        var createdAt = flow.CreatedAt;
                        if (createdAt == default)
                        {
                            try
                            {
                                createdAt = File.GetCreationTimeUtc(file);
                            }
                            catch { /* ignore */ }
                        }
                        // Normalize to local time for display
                        if (createdAt.Kind == DateTimeKind.Utc)
                        {
                            createdAt = createdAt.ToLocalTime();
                        }
                        _flows.Add(new FlowListItem
                        {
                            Id = flow.Id,
                            Name = flow.Name,
                            Description = flow.Description,
                            Enabled = flow.Enabled,
                            CreatedAt = createdAt,
                            TriggerType = flow.Triggers.FirstOrDefault()?.Type ?? "なし",
                            FilePath = file
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load flow: {File}", file);
                }
            }
            
            // Refresh view to apply sorting/filtering
            _flowsView?.Refresh();
            
            UpdateStatus($"{_flows.Count} 個のフローを読み込みました", StatusType.Success);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load flows");
            UpdateStatus("フローの読み込みに失敗しました", StatusType.Error);
        }
    }

    // Apply text-based filter for Saved Flows
    private bool FlowFilter(object obj)
    {
        if (obj is not FlowListItem item) return false;
        var q = SavedFlowsSearchBox?.Text?.Trim();
        if (string.IsNullOrEmpty(q)) return true;
        q = q.ToLowerInvariant();
        return (item.Name?.ToLowerInvariant().Contains(q) ?? false)
               || (item.Description?.ToLowerInvariant().Contains(q) ?? false)
               || (item.TriggerType?.ToLowerInvariant().Contains(q) ?? false);
    }

    private void SavedFlowsSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (SavedFlowsSearchBox != null)
        {
            _settingsService.SavedFlowsSearchText = SavedFlowsSearchBox.Text;
        }
        _flowsView?.Refresh();
    }

    private void SavedFlowsExpander_Expanded(object sender, RoutedEventArgs e)
    {
        _settingsService.SavedFlowsExpanded = true;
    }

    private void SavedFlowsExpander_Collapsed(object sender, RoutedEventArgs e)
    {
        _settingsService.SavedFlowsExpanded = false;
    }

    private void RefreshFlows_Click(object sender, RoutedEventArgs e)
    {
        LoadSavedFlows();
        UpdateStatus("保存済みフローを更新しました", StatusType.Success);
    }

    private void ClearSearch_Click(object sender, RoutedEventArgs e)
    {
        if (SavedFlowsSearchBox != null)
        {
            SavedFlowsSearchBox.Text = string.Empty;
        }
    }
    
    private void OnComponentConfigured(object sender, ComponentConfiguredEventArgs e)
    {
        try
        {
            // Ensure a flow is active
            if (_currentFlowBuilder == null)
            {
                _currentFlowBuilder = _flowComposerBuilder.StartFlow("新規フロー");
                UpdateStatus("新規フローを開始しました", StatusType.Success);
            }

            // Create component
            var flowComponent = new FlowComponent
            {
                Id = Guid.NewGuid().ToString(),
                ComponentId = e.Component.Id,
                Type = e.Component.Type,
                Parameters = e.Parameters,
                Order = _currentFlowComponents.Count
            };

            // Create and execute command
            var command = new AddFlowComponentCommand(
                _currentFlowComponents,
                flowComponent,
                _currentFlowBuilder,
                UpdateFlowPreview);
                
            _commandManager.ExecuteCommand(command);
            
            UpdateStatus($"{e.Component.Name} を追加しました", StatusType.Success);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add component");
            UpdateStatus("コンポーネントの追加に失敗しました", StatusType.Error);
        }
    }
    
    private void UpdateFlowPreview()
    {
        if (FlowPreview == null) return;
        
        FlowPreview.Children.Clear();
        
        foreach (var component in _currentFlowComponents)
        {
            var componentDef = _flowComposerBuilder.GetComponent(component.ComponentId);
            if (componentDef == null) continue;
            
            // Create preview card
            var card = new Border
            {
                CornerRadius = new CornerRadius(5),
                Margin = new Thickness(0, 0, 0, 10),
                Padding = new Thickness(10)
            };

            // Set background via theme resource so it updates on theme changes
            var brushKey = component.Type switch
            {
                ComponentType.Trigger => "TriggerCardBrush",
                ComponentType.Condition => "ConditionCardBrush",
                ComponentType.Action => "ActionCardBrush",
                _ => "SecondaryBackgroundBrush"
            };
            card.SetResourceReference(Border.BackgroundProperty, brushKey);
            
            var stack = new StackPanel();
            
            // Header
            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var headerStack = new StackPanel { Orientation = Orientation.Horizontal };
            headerStack.Children.Add(new TextBlock
            {
                Text = componentDef.Icon,
                FontSize = 20,
                Margin = new Thickness(0, 0, 10, 0)
            });
            headerStack.Children.Add(new TextBlock
            {
                Text = componentDef.Name,
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center
            });

            Grid.SetColumn(headerStack, 0);
            headerGrid.Children.Add(headerStack);

            var deleteButton = new Button
            {
                Content = "🗑️",
                Tag = component,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                FontSize = 14,
                ToolTip = "このコンポーネントを削除します",
                Cursor = Cursors.Hand
            };
            deleteButton.Click += RemoveComponent_Click;

            Grid.SetColumn(deleteButton, 1);
            headerGrid.Children.Add(deleteButton);

            stack.Children.Add(headerGrid);
            
            // Parameters
            if (component.Parameters.Any())
            {
                var paramsText = new TextBlock
                {
                    Text = string.Join(", ", component.Parameters.Select(p => $"{p.Key}: {p.Value}")),
                    FontSize = 11,
                    Foreground = Brushes.DarkGray,
                    Margin = new Thickness(30, 5, 0, 0)
                };
                stack.Children.Add(paramsText);
            }
            
            card.Child = stack;
            FlowPreview.Children.Add(card);
        }
        
        if (!_currentFlowComponents.Any())
        {
            var emptyText = new TextBlock
            {
                Text = "コンポーネントを選択して追加してください",
                Foreground = Brushes.Gray,
                FontStyle = FontStyles.Italic,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 50, 0, 0)
            };
            FlowPreview.Children.Add(emptyText);
        }
    }
    
    private Color GetComponentColor(ComponentType type)
    {
        return type switch
        {
            ComponentType.Trigger => Color.FromRgb(255, 243, 224),
            ComponentType.Condition => Color.FromRgb(224, 255, 255),
            ComponentType.Action => Color.FromRgb(224, 255, 224),
            _ => Colors.White
        };
    }
    
    private void NewFlow_Click(object sender, RoutedEventArgs e)
    {
        // Prompt for flow name
        var flowName = PromptForFlowName("新規フロー");
        if (string.IsNullOrWhiteSpace(flowName))
        {
            return; // User cancelled
        }
        
        // Validate flow name
        var validation = _validationService.ValidateRequired(flowName, "フロー名");
        if (!validation.IsValid)
        {
            MessageBox.Show(validation.ErrorMessage, "エラー", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        
        _currentFlowComponents.Clear();
        _currentFlowBuilder = _flowComposerBuilder.StartFlow(flowName);
        UpdateFlowPreview();
        UpdateStatus($"フロー '{flowName}' を作成しました", StatusType.Success);
    }
    
    /// <summary>
    /// Prompts the user for a flow name
    /// </summary>
    /// <param name="defaultName">Default name to show in the prompt</param>
    /// <returns>Flow name or null if cancelled</returns>
    private string PromptForFlowName(string defaultName)
    {
        var inputDialog = new InputDialog("フロー名を入力してください", "フロー名", defaultName);
        if (inputDialog.ShowDialog() == true)
        {
            return inputDialog.InputText;
        }
        return null;
    }
    
    private void OpenFlow_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Flow files (*.json)|*.json|All files (*.*)|*.*",
            DefaultExt = ".json"
        };
        
        if (dialog.ShowDialog() == true)
        {
            try
            {
                var json = File.ReadAllText(dialog.FileName);
                var loaded = JsonSerializer.Deserialize<FlowDefinition>(json);
                if (loaded != null)
                {
                    LoadFlow(loaded);
                }
                UpdateStatus($"フローを開きました: {Path.GetFileName(dialog.FileName)}", StatusType.Success);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open flow");
                MessageBox.Show($"フローを開けませんでした: {ex.Message}", "エラー", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
    
    private void SaveFlow_Click(object sender, RoutedEventArgs e)
    {
        if (_currentFlowBuilder == null)
        {
            MessageBox.Show("保存するフローがありません", "警告", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        
        // Validate flow name
        var flow = _currentFlowBuilder.Build();
        var validation = _validationService.ValidateRequired(flow.Name, "フロー名");
        if (!validation.IsValid)
        {
            MessageBox.Show(validation.ErrorMessage, "エラー", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        
        try
        {
            var json = JsonSerializer.Serialize(flow, new JsonSerializerOptions { WriteIndented = true });
            
            var dialog = new SaveFileDialog
            {
                Filter = "Flow files (*.json)|*.json|All files (*.*)|*.*",
                DefaultExt = ".json",
                FileName = $"{flow.Name}.json"
            };
            
            if (dialog.ShowDialog() == true)
            {
                File.WriteAllText(dialog.FileName, json);
                UpdateStatus($"フローを保存しました: {Path.GetFileName(dialog.FileName)}", StatusType.Success);
                
                // Reload flows
                LoadSavedFlows();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save flow");
            MessageBox.Show($"フローの保存に失敗しました: {ex.Message}", "エラー", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    private async void RunFlow_Click(object sender, RoutedEventArgs e)
    {
        if (_currentFlowBuilder == null)
        {
            MessageBox.Show("実行するフローがありません", "警告", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        
        try
        {
            var flow = _currentFlowBuilder.Build();

            // Log selected model (by ID) for verification
            var selectedModel = GetSelectedModelInfo();
            if (selectedModel != null)
            {
                _logger.LogInformation("Using model: {Name} (Id={Id})", selectedModel.Name, selectedModel.Id);
            }
            else
            {
                _logger.LogWarning("No valid model resolved from settings (Name='{Name}', Id='{Id}')",
                    _settingsService.SelectedModel, _settingsService.SelectedModelId);
            }

            // Convert to Automation DSL Rule
            var rule = ConvertFlowToAutomationRule(flow);

            // Inject selected stable model ID into all LLM actions (llmQuery)
            try
            {
                var resolvedModel = GetSelectedModelInfo();
                if (resolvedModel != null && rule?.Actions != null)
                {
                    bool hasLlm = false;
                    foreach (var act in rule.Actions)
                    {
                        if (string.Equals(act?.Type, "llmQuery", StringComparison.OrdinalIgnoreCase))
                        {
                            hasLlm = true;
                            act.Parameters ??= new Dictionary<string, object>();
                            act.Parameters["modelId"] = resolvedModel.Id;
                        }
                    }
                    if (hasLlm && rule.Permissions != null)
                    {
                        rule.Permissions.Llm = true;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to inject modelId into LLM actions");
            }

            // Serialize to JSON
            var json = JsonSerializer.Serialize(rule);

            // Validate JSON
            UpdateStatus($"フロー '{flow.Name}' を検証中...", StatusType.Running);
            var validateSw = Stopwatch.StartNew();
            var validation = await _automationService.ValidateRuleJsonAsync(json);
            validateSw.Stop();
            _logger.LogInformation("UI ValidateRuleJsonAsync completed in {DurationMs} ms for rule Id={RuleId} Name={Name}", validateSw.ElapsedMilliseconds, rule?.Id, rule?.Name);
            if (!validation.IsValid)
            {
                UpdateStatus("検証に失敗しました", StatusType.Error);
                MessageBox.Show("ルールが無効です。設定を確認してください。", "エラー",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Add and run the rule
            UpdateStatus($"フロー '{flow.Name}' を実行中...", StatusType.Running);
            var addSw = Stopwatch.StartNew();
            var added = await _automationService.AddRuleFromJsonAsync(json);
            addSw.Stop();
            _logger.LogInformation("UI AddRuleFromJsonAsync completed in {DurationMs} ms; Added={Added} for rule Id={RuleId} Name={Name}", addSw.ElapsedMilliseconds, added, rule?.Id, rule?.Name);
            if (!added)
            {
                UpdateStatus("ルールの追加に失敗しました", StatusType.Error);
                MessageBox.Show("ルールの追加に失敗しました。", "エラー",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Update running flows count
            if (RunningFlowsCount != null)
            {
                RunningFlowsCount.Text = "1";
            }

            UpdateStatus($"フロー '{flow.Name}' の実行を開始しました", StatusType.Success);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to run flow");
            UpdateStatus("フローの実行に失敗しました", StatusType.Error);
            MessageBox.Show($"フローの実行に失敗しました: {ex.Message}", "エラー", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    // Helper to convert FlowDefinition (UI model) to Automation DSL Rule (engine model)
    private AutomationDsl.Rule ConvertFlowToAutomationRule(FlowDefinition flow)
    {
        var rule = new AutomationDsl.Rule
        {
            Id = flow.Id,
            Name = flow.Name,
            Description = flow.Description,
            Enabled = flow.Enabled,
            Variables = flow.Variables ?? new Dictionary<string, object>()
        };

        // Trigger (engine expects a single trigger)
        var firstTrigger = flow.Triggers?.FirstOrDefault();
        if (firstTrigger != null)
        {
            rule.Trigger = new AutomationDsl.TriggerDefinition
            {
                Type = firstTrigger.Type,
                Parameters = firstTrigger.Parameters ?? new Dictionary<string, object>()
            };
        }
        else
        {
            // Fallback to manual trigger to satisfy schema requirements
            rule.Trigger = new AutomationDsl.TriggerDefinition
            {
                Type = "manual",
                Parameters = new Dictionary<string, object>()
            };
        }

        // Conditions
        if (flow.Conditions != null)
        {
            foreach (var c in flow.Conditions)
            {
                rule.Conditions.Add(new AutomationDsl.ConditionDefinition
                {
                    Type = c.Type,
                    Parameters = c.Parameters ?? new Dictionary<string, object>(),
                    Negate = c.Negate
                });
            }
        }

        // Actions
        if (flow.Actions != null)
        {
            foreach (var a in flow.Actions)
            {
                rule.Actions.Add(new AutomationDsl.ActionDefinition
                {
                    Type = a.Type,
                    Parameters = a.Parameters ?? new Dictionary<string, object>(),
                    ContinueOnError = a.ContinueOnError,
                    RetryCount = a.RetryCount,
                    TimeoutMs = a.TimeoutMs
                });
            }
        }

        // Permissions
        rule.Permissions = new AutomationDsl.PermissionSet
        {
            Network = flow.Permissions?.Network ?? false,
            FileSystem = flow.Permissions?.FileSystem ?? false,
            Shell = flow.Permissions?.Shell ?? false,
            Llm = flow.Permissions?.Llm ?? false,
            AllowedDomains = flow.Permissions?.AllowedDomains ?? new List<string>(),
            AllowedPaths = flow.Permissions?.AllowedPaths ?? new List<string>()
        };

        // Metadata
        rule.Metadata = new AutomationDsl.RuleMetadata
        {
            CreatedAt = flow.CreatedAt,
            UpdatedAt = flow.UpdatedAt,
            Version = TryGetString(flow.Metadata, "version") ?? "1.0.0",
            Author = TryGetString(flow.Metadata, "author"),
            Tags = TryGetStringList(flow.Metadata, "tags"),
            Source = TryGetString(flow.Metadata, "source") ?? "Loco.UI"
        };

        return rule;
    }

    private static string TryGetString(Dictionary<string, object> dict, string key)
    {
        if (dict != null && dict.TryGetValue(key, out var v) && v != null)
        {
            return v as string ?? v.ToString();
        }
        return null;
    }

    private static List<string> TryGetStringList(Dictionary<string, object> dict, string key)
    {
        var list = new List<string>();
        if (dict == null || !dict.TryGetValue(key, out var v) || v == null) return list;

        if (v is System.Text.Json.JsonElement je)
        {
            if (je.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var el in je.EnumerateArray())
                {
                    if (el.ValueKind == System.Text.Json.JsonValueKind.String) list.Add(el.GetString());
                }
                return list;
            }
            if (je.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                var s = je.GetString();
                if (!string.IsNullOrWhiteSpace(s))
                {
                    list = s.Split(',', StringSplitOptions.RemoveEmptyEntries)
                            .Select(t => t.Trim()).ToList();
                }
                return list;
            }
        }

        if (v is IEnumerable<string> es)
        {
            list.AddRange(es);
            return list;
        }

        if (v is IEnumerable<object> eo)
        {
            foreach (var item in eo)
            {
                if (item != null) list.Add(item.ToString());
            }
            return list;
        }

        var str = v as string ?? v.ToString();
        if (!string.IsNullOrWhiteSpace(str))
        {
            list = str.Split(',', StringSplitOptions.RemoveEmptyEntries)
                      .Select(t => t.Trim()).ToList();
        }
        return list;
    }

    private void EditFlow_Click(object sender, RoutedEventArgs e)
    {
        var flow = (sender as Button)?.DataContext as FlowListItem
                   ?? SavedFlowsList?.SelectedItem as FlowListItem;
        if (flow == null) return;

        try
        {
            var json = File.ReadAllText(flow.FilePath);
            var loaded = JsonSerializer.Deserialize<FlowDefinition>(json);
            if (loaded != null)
            {
                LoadFlow(loaded);
            }
            UpdateStatus($"フローを編集中: {flow.Name}", StatusType.Success);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to edit flow");
        }
    }
    
    private void DuplicateFlow_Click(object sender, RoutedEventArgs e)
    {
        var flow = (sender as Button)?.DataContext as FlowListItem
                   ?? SavedFlowsList?.SelectedItem as FlowListItem;
        if (flow == null) return;

        try
        {
            var json = File.ReadAllText(flow.FilePath);
            var originalFlow = JsonSerializer.Deserialize<FlowDefinition>(json);
            if (originalFlow != null)
            {
                originalFlow.Id = Guid.NewGuid().ToString();
                originalFlow.Name = $"{originalFlow.Name} (コピー)";
                originalFlow.CreatedAt = DateTime.UtcNow;
                originalFlow.UpdatedAt = DateTime.UtcNow;
                
                var newJson = JsonSerializer.Serialize(originalFlow, new JsonSerializerOptions { WriteIndented = true });
                var newPath = Path.Combine(
                    Path.GetDirectoryName(flow.FilePath),
                    $"{originalFlow.Id}.json");
                
                File.WriteAllText(newPath, newJson);
                LoadSavedFlows();
                UpdateStatus($"フローを複製しました: {originalFlow.Name}", StatusType.Success);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to duplicate flow");
        }
    }
    
    private void DeleteFlow_Click(object sender, RoutedEventArgs e)
    {
        var flow = (sender as Button)?.DataContext as FlowListItem
                   ?? SavedFlowsList?.SelectedItem as FlowListItem;
        if (flow == null) return;

        var result = MessageBox.Show(
            $"フロー '{flow.Name}' を削除しますか？",
            "確認",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        
        if (result == MessageBoxResult.Yes)
        {
            try
            {
                File.Delete(flow.FilePath);
                _flows.Remove(flow);
                _flowsView?.Refresh();
                UpdateStatus($"フローを削除しました: {flow.Name}", StatusType.Success);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete flow");
            }
        }
    }

    private void SavedFlowsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (SavedFlowsList?.SelectedItem is FlowListItem flow)
        {
            try
            {
                var json = File.ReadAllText(flow.FilePath);
                var loaded = JsonSerializer.Deserialize<FlowDefinition>(json);
                if (loaded != null)
                {
                    LoadFlow(loaded);
                    UpdateStatus($"フローを編集中: {flow.Name}", StatusType.Success);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open flow from double-click");
            }
        }
    }
    
    private async void ConvertNaturalLanguage_Click(object sender, RoutedEventArgs e)
    {
        // Validate input using the validation service
        var validation = _validationService.ValidateRequired(NaturalLanguageInput?.Text, "自然言語入力");
        if (!validation.IsValid)
        {
            MessageBox.Show(validation.ErrorMessage, "警告", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        
        try
        {
            UpdateStatus("自然言語を変換中...", StatusType.Running);
            
            // Convert to Automation DSL JSON via service
            var json = await _nlService.ConvertTextToRuleJsonAsync(NaturalLanguageInput.Text);
            if (string.IsNullOrWhiteSpace(json))
            {
                UpdateStatus("変換に失敗しました", StatusType.Error);
                MessageBox.Show("変換に失敗しました。入力内容を見直してください。", "エラー",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            
            // Inject selected stable model ID into any LLM actions (for NL-generated rules)
            try
            {
                var resolvedModel = GetSelectedModelInfo();
                if (resolvedModel != null)
                {
                    var rule = JsonSerializer.Deserialize<AutomationDsl.Rule>(json);
                    if (rule?.Actions != null)
                    {
                        bool hasLlm = false;
                        foreach (var act in rule.Actions)
                        {
                            if (string.Equals(act?.Type, "llmQuery", StringComparison.OrdinalIgnoreCase))
                            {
                                hasLlm = true;
                                act.Parameters ??= new Dictionary<string, object>();
                                act.Parameters["modelId"] = resolvedModel.Id;
                            }
                        }
                        if (hasLlm && rule.Permissions != null)
                        {
                            rule.Permissions.Llm = true;
                        }
                        json = JsonSerializer.Serialize(rule);
                    }
                }
                else
                {
                    _logger.LogWarning("No valid model resolved when injecting into NL-generated rule");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to inject modelId into NL-generated LLM actions");
            }
            
            // Validate and persist via AutomationService
            var ruleValidation = await _automationService.ValidateRuleJsonAsync(json);
            if (!ruleValidation.IsValid)
            {
                UpdateStatus("検証に失敗しました", StatusType.Error);
                MessageBox.Show("生成したルールが無効です。", "エラー",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            
            var added = await _automationService.AddRuleFromJsonAsync(json);
            if (!added)
            {
                UpdateStatus("保存に失敗しました", StatusType.Error);
                MessageBox.Show("ルールの保存に失敗しました。", "エラー",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            
            UpdateStatus("自然言語の変換が完了しました", StatusType.Success);
            MessageBox.Show("自然言語からルールを生成・保存しました。", "完了",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to convert natural language");
            UpdateStatus("変換に失敗しました", StatusType.Error);
        }
    }
    
    private void RemoveComponent_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is FlowComponent component)
        {
            try
            {
                var command = new RemoveFlowComponentCommand(
                    _currentFlowComponents,
                    component,
                    _currentFlowBuilder,
                    UpdateFlowPreview);

                _commandManager.ExecuteCommand(command);

                UpdateStatus($"{component.Type} コンポーネントを削除しました", StatusType.Success);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to remove component");
                UpdateStatus("コンポーネントの削除に失敗しました", StatusType.Error);
            }
        }
    }

    private string _lastStatusMessage;
    private StatusType _lastStatusType;

    private void UpdateStatus(string message, StatusType type)
    {
        if (StatusText != null)
        {
            StatusText.Text = message;
            _lastStatusMessage = message;
            _lastStatusType = type;

            // Use DynamicResource so brush updates on theme change
            var resourceKey = type switch
            {
                StatusType.Ready => "SuccessBrush",
                StatusType.Running => "WarningBrush",
                StatusType.Success => "SuccessBrush",
                StatusType.Error => "ErrorBrush",
                _ => "InfoBrush"
            };
            StatusText.SetResourceReference(TextBlock.ForegroundProperty, resourceKey);
        }
    }

    private void OnThemeChanged(object sender, ThemeChangedEventArgs e)
    {
        // Reapply status to ensure resource references are correct
        if (!string.IsNullOrEmpty(_lastStatusMessage))
        {
            UpdateStatus(_lastStatusMessage, _lastStatusType);
        }

        // Keep ThemeSelector in sync with the current theme
        if (ThemeSelector != null)
        {
            var current = ThemeManager.CurrentTheme.ToString();
            foreach (ComboBoxItem item in ThemeSelector.Items)
            {
                var tag = item.Tag?.ToString();
                if (!string.IsNullOrWhiteSpace(tag) &&
                    string.Equals(tag, current, StringComparison.OrdinalIgnoreCase))
                {
                    ThemeSelector.SelectedItem = item;
                    break;
                }
            }
        }
    }
    
    protected override void OnClosed(EventArgs e)
    {
        // Prevent memory leaks from event subscriptions
        ThemeManager.ThemeChanged -= OnThemeChanged;
        _modelManager.ModelsChanged -= OnModelsChanged;
        base.OnClosed(e);
    }
    
    private enum StatusType
    {
        Ready,
        Running,
        Success,
        Error
    }
    
    private void LoadFlow(FlowDefinition flow)
    {
        _currentFlowComponents.Clear();
        _currentFlowBuilder = _flowComposerBuilder.StartFlow(flow.Name, flow.Description);
        // Triggers
        foreach (var t in flow.Triggers)
        {
            _currentFlowBuilder.AddTrigger(t.Type, t.Parameters);
            _currentFlowComponents.Add(new FlowComponent
            {
                Id = t.Id,
                ComponentId = t.Type,
                Type = ComponentType.Trigger,
                Parameters = t.Parameters,
                Order = _currentFlowComponents.Count
            });
        }
        // Conditions
        foreach (var c in flow.Conditions)
        {
            _currentFlowBuilder.AddCondition(c.Type, c.Parameters);
            _currentFlowComponents.Add(new FlowComponent
            {
                Id = c.Id,
                ComponentId = c.Type,
                Type = ComponentType.Condition,
                Parameters = c.Parameters,
                Order = _currentFlowComponents.Count
            });
        }
        // Actions
        foreach (var a in flow.Actions)
        {
            _currentFlowBuilder.AddAction(a.Type, a.Parameters);
            _currentFlowComponents.Add(new FlowComponent
            {
                Id = a.Id,
                ComponentId = a.Type,
                Type = ComponentType.Action,
                Parameters = a.Parameters,
                Order = _currentFlowComponents.Count
            });
        }
        UpdateFlowPreview();
    }
    
    private class FlowListItem
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool Enabled { get; set; }
        public DateTime CreatedAt { get; set; }
        public string TriggerType { get; set; }
        public string FilePath { get; set; }
    }

    private class FlowComponent
    {
        public string Id { get; set; }
        public string ComponentId { get; set; }
        public ComponentType Type { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new();
        public int Order { get; set; }
    }
}

/// <summary>
/// A command whose sole purpose is to relay its functionality to other objects by invoking delegates.
/// The default return value for the CanExecute method is 'true'.
/// </summary>
public class RelayCommand : System.Windows.Input.ICommand
{
    private readonly Action<object> _execute;
    private readonly Predicate<object> _canExecute;

    public RelayCommand(Action<object> execute, Predicate<object> canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public bool CanExecute(object parameter)
    {
        return _canExecute?.Invoke(parameter) ?? true;
    }

    public void Execute(object parameter)
    {
        _execute(parameter);
    }

    public event EventHandler CanExecuteChanged
    {
        add { System.Windows.Input.CommandManager.RequerySuggested += value; }
        remove { System.Windows.Input.CommandManager.RequerySuggested -= value; }
    }
}
