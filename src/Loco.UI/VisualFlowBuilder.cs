using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Loco.Core.Models;

namespace Loco.UI.FlowBuilder;

/// <summary>
/// Visual flow builder
/// Drag & drop interface for building automation flows
/// </summary>
public class VisualFlowBuilder : UserControl
{
    private readonly Canvas _canvas;
    private readonly ListBox _toolbox;
    private readonly PropertyGrid _propertyGrid;
    private readonly ObservableCollection<FlowNode> _nodes = new();
    private readonly ObservableCollection<FlowConnection> _connections = new();
    private FlowNode _selectedNode;
    private System.Windows.Point _dragStartPoint;
    private bool _isDragging;

    public VisualFlowBuilder()
    {
        InitializeUI();
        LoadToolbox();
    }

    /// <summary>
    /// Current flow being edited
    /// </summary>
    public AutomationDsl.Rule CurrentRule { get; private set; } = new();

    /// <summary>
    /// Initialize UI components
    /// </summary>
    private void InitializeUI()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(250) }); // Toolbox
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Canvas
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) }); // Properties

        // Toolbox panel
        _toolbox = new ListBox
        {
            Background = new SolidColorBrush(Color.FromRgb(45, 45, 48)),
            BorderThickness = new Thickness(0)
        };
        Grid.SetColumn(_toolbox, 0);

        // Canvas for flow design
        _canvas = new Canvas
        {
            Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
            AllowDrop = true
        };
        _canvas.Drop += Canvas_Drop;
        _canvas.DragOver += Canvas_DragOver;
        _canvas.MouseLeftButtonDown += Canvas_MouseLeftButtonDown;
        _canvas.MouseMove += Canvas_MouseMove;
        _canvas.MouseLeftButtonUp += Canvas_MouseLeftButtonUp;
        
        var canvasScroll = new ScrollViewer
        {
            Content = _canvas,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        Grid.SetColumn(canvasScroll, 1);

        // Property grid
        _propertyGrid = new PropertyGrid();
        Grid.SetColumn(_propertyGrid, 2);

        grid.Children.Add(_toolbox);
        grid.Children.Add(canvasScroll);
        grid.Children.Add(_propertyGrid);

        Content = grid;
    }

    /// <summary>
    /// Load toolbox with available components
    /// </summary>
    private void LoadToolbox()
    {
        var categories = new Dictionary<string, List<ToolboxItem>>
        {
            ["トリガー"] = new List<ToolboxItem>
            {
                new ToolboxItem("時刻", "time", "⏰", "指定時刻や間隔で実行"),
                new ToolboxItem("ファイル変更", "fileSystem", "📁", "ファイルの作成・変更を検知"),
                new ToolboxItem("アプリ起動", "application", "🚀", "アプリの起動・終了を検知"),
                new ToolboxItem("Webhook", "webhook", "🌐", "HTTP要求を受信"),
                new ToolboxItem("システムイベント", "systemEvent", "💻", "バッテリー、ネットワーク等"),
                new ToolboxItem("通知受信", "notification", "🔔", "通知を受信したとき"),
                new ToolboxItem("場所", "location", "📍", "特定の場所に入った/出た"),
                new ToolboxItem("Wi-Fi", "wifi", "📶", "Wi-Fi接続/切断")
            },
            ["条件"] = new List<ToolboxItem>
            {
                new ToolboxItem("時間範囲", "timeRange", "⏱️", "特定の時間帯のみ"),
                new ToolboxItem("曜日", "dayOfWeek", "📅", "特定の曜日のみ"),
                new ToolboxItem("ファイル存在", "fileExists", "✅", "ファイルが存在する場合"),
                new ToolboxItem("変数比較", "variable", "🔢", "変数の値を比較"),
                new ToolboxItem("ネットワーク状態", "network", "🌐", "オンライン/オフライン"),
                new ToolboxItem("バッテリー", "battery", "🔋", "バッテリー残量条件"),
                new ToolboxItem("実行中アプリ", "runningApp", "▶️", "特定アプリが実行中"),
                new ToolboxItem("画面状態", "screen", "📱", "画面ON/OFF")
            },
            ["アクション"] = new List<ToolboxItem>
            {
                new ToolboxItem("通知表示", "notification", "💬", "デスクトップ通知を表示"),
                new ToolboxItem("ファイル操作", "file", "📄", "ファイル読み書き・移動"),
                new ToolboxItem("HTTP要求", "httpRequest", "🔗", "Web API呼び出し"),
                new ToolboxItem("アプリ起動", "launchApp", "▶️", "プログラムを実行"),
                new ToolboxItem("音声読み上げ", "tts", "🔊", "テキストを音声で読み上げ"),
                new ToolboxItem("LLM処理", "llmQuery", "🤖", "AI処理を実行"),
                new ToolboxItem("待機", "wait", "⏸️", "指定時間待機"),
                new ToolboxItem("変数設定", "setVariable", "📝", "変数に値を設定"),
                new ToolboxItem("メール送信", "email", "📧", "メールを送信"),
                new ToolboxItem("音を鳴らす", "sound", "🔔", "サウンドを再生"),
                new ToolboxItem("スクリーンショット", "screenshot", "📸", "画面キャプチャ"),
                new ToolboxItem("クリップボード", "clipboard", "📋", "コピー/貼り付け")
            },
            ["制御"] = new List<ToolboxItem>
            {
                new ToolboxItem("条件分岐", "if", "🔀", "条件による分岐"),
                new ToolboxItem("ループ", "loop", "🔁", "繰り返し処理"),
                new ToolboxItem("並列実行", "parallel", "⚡", "複数アクション同時実行"),
                new ToolboxItem("エラー処理", "try", "⚠️", "エラー時の処理"),
                new ToolboxItem("停止", "stop", "🛑", "フロー実行を停止"),
                new ToolboxItem("別フロー実行", "callFlow", "📞", "他のフローを呼び出し")
            }
        };

        foreach (var category in categories)
        {
            var expander = new Expander
            {
                Header = category.Key,
                IsExpanded = true,
                Margin = new Thickness(5)
            };

            var itemsPanel = new StackPanel();
            
            foreach (var item in category.Value)
            {
                var button = new Button
                {
                    Content = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Children =
                        {
                            new TextBlock { Text = item.Icon, FontSize = 20, Margin = new Thickness(5) },
                            new StackPanel
                            {
                                Children =
                                {
                                    new TextBlock { Text = item.Name, FontWeight = FontWeights.Bold },
                                    new TextBlock { Text = item.Description, FontSize = 10, Foreground = Brushes.Gray }
                                }
                            }
                        }
                    },
                    Tag = item,
                    Margin = new Thickness(2),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Background = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(5),
                    Cursor = System.Windows.Input.Cursors.Hand
                };
                
                button.Click += ToolboxItem_Click;
                button.MouseMove += ToolboxItem_MouseMove;
                
                itemsPanel.Children.Add(button);
            }
            
            expander.Content = itemsPanel;
            _toolbox.Items.Add(expander);
        }
    }

    /// <summary>
    /// Handle toolbox item click
    /// </summary>
    private void ToolboxItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is ToolboxItem item)
        {
            ShowItemWizard(item);
        }
    }

    /// <summary>
    /// Show configuration wizard for item
    /// </summary>
    private void ShowItemWizard(ToolboxItem item)
    {
        var wizard = new FlowItemWizard(item);
        if (wizard.ShowDialog() == true)
        {
            var node = CreateNode(item, wizard.Configuration);
            AddNodeToCanvas(node);
        }
    }

    /// <summary>
    /// Create flow node from toolbox item
    /// </summary>
    private FlowNode CreateNode(ToolboxItem item, Dictionary<string, object> config)
    {
        return new FlowNode
        {
            Id = Guid.NewGuid().ToString(),
            Type = item.Type,
            Name = item.Name,
            Icon = item.Icon,
            Category = GetCategoryFromItem(item),
            Configuration = config,
            Position = new System.Windows.Point(_canvas.ActualWidth / 2, _canvas.ActualHeight / 2)
        };
    }

    /// <summary>
    /// Add node to canvas
    /// </summary>
    private void AddNodeToCanvas(FlowNode node)
    {
        _nodes.Add(node);
        
        var visual = CreateNodeVisual(node);
        Canvas.SetLeft(visual, node.Position.X);
        Canvas.SetTop(visual, node.Position.Y);
        
        _canvas.Children.Add(visual);
        
        UpdateRule();
    }

    /// <summary>
    /// Create visual representation of node
    /// </summary>
    private UIElement CreateNodeVisual(FlowNode node)
    {
        var border = new Border
        {
            Background = GetNodeColor(node.Category),
            CornerRadius = new CornerRadius(8),
            BorderBrush = Brushes.White,
            BorderThickness = new Thickness(2),
            Width = 180,
            Height = 80,
            Tag = node,
            Cursor = System.Windows.Input.Cursors.Hand
        };

        var content = new Grid();
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // Header
        var header = new TextBlock
        {
            Text = $"{node.Icon} {node.Name}",
            Foreground = Brushes.White,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(10, 5, 10, 0),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        Grid.SetRow(header, 0);
        content.Children.Add(header);

        // Details
        var details = new TextBlock
        {
            Text = GetNodeSummary(node),
            Foreground = Brushes.LightGray,
            FontSize = 10,
            Margin = new Thickness(10, 0, 10, 5),
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetRow(details, 1);
        content.Children.Add(details);

        // Connection points
        AddConnectionPoints(border);

        border.Child = content;
        
        border.MouseLeftButtonDown += Node_MouseLeftButtonDown;
        border.MouseRightButtonDown += Node_MouseRightButtonDown;
        
        return border;
    }

    /// <summary>
    /// Get node background color by category
    /// </summary>
    private Brush GetNodeColor(string category)
    {
        return category switch
        {
            "trigger" => new SolidColorBrush(Color.FromRgb(46, 125, 50)),  // Green
            "condition" => new SolidColorBrush(Color.FromRgb(251, 140, 0)), // Orange
            "action" => new SolidColorBrush(Color.FromRgb(33, 150, 243)),  // Blue
            "control" => new SolidColorBrush(Color.FromRgb(156, 39, 176)), // Purple
            _ => new SolidColorBrush(Color.FromRgb(96, 96, 96))
        };
    }

    /// <summary>
    /// Get summary text for node
    /// </summary>
    private string GetNodeSummary(FlowNode node)
    {
        if (node.Configuration == null)
            return "";

        return node.Type switch
        {
            "time" => node.Configuration.TryGetValue("time", out var time) ? $"毎日 {time}" : "時刻設定",
            "fileSystem" => node.Configuration.TryGetValue("path", out var path) ? $"監視: {path}" : "パス設定",
            "httpRequest" => node.Configuration.TryGetValue("url", out var url) ? $"{url}" : "URL設定",
            "notification" => node.Configuration.TryGetValue("title", out var title) ? $"{title}" : "通知設定",
            _ => "クリックして設定"
        };
    }

    /// <summary>
    /// Add connection points to node
    /// </summary>
    private void AddConnectionPoints(Border border)
    {
        // Input point (top)
        var inputPoint = new Ellipse
        {
            Width = 12,
            Height = 12,
            Fill = Brushes.Yellow,
            Stroke = Brushes.Black,
            StrokeThickness = 1,
            Margin = new Thickness(84, -6, 0, 0),
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Left,
            Cursor = System.Windows.Input.Cursors.Cross
        };
        
        // Output point (bottom)
        var outputPoint = new Ellipse
        {
            Width = 12,
            Height = 12,
            Fill = Brushes.Yellow,
            Stroke = Brushes.Black,
            StrokeThickness = 1,
            Margin = new Thickness(84, 0, 0, -6),
            VerticalAlignment = VerticalAlignment.Bottom,
            HorizontalAlignment = HorizontalAlignment.Left,
            Cursor = System.Windows.Input.Cursors.Cross
        };

        var grid = new Grid();
        grid.Children.Add(border);
        grid.Children.Add(inputPoint);
        grid.Children.Add(outputPoint);
    }

    /// <summary>
    /// Handle node selection
    /// </summary>
    private void Node_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is Border border && border.Tag is FlowNode node)
        {
            SelectNode(node);
            _dragStartPoint = e.GetPosition(_canvas);
            _isDragging = true;
            border.CaptureMouse();
        }
    }

    /// <summary>
    /// Handle node context menu
    /// </summary>
    private void Node_MouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is Border border && border.Tag is FlowNode node)
        {
            var menu = new ContextMenu();
            
            var editItem = new MenuItem { Header = "編集", Icon = new TextBlock { Text = "✏️" } };
            editItem.Click += (s, args) => EditNode(node);
            menu.Items.Add(editItem);
            
            var duplicateItem = new MenuItem { Header = "複製", Icon = new TextBlock { Text = "📋" } };
            duplicateItem.Click += (s, args) => DuplicateNode(node);
            menu.Items.Add(duplicateItem);
            
            menu.Items.Add(new Separator());
            
            var deleteItem = new MenuItem { Header = "削除", Icon = new TextBlock { Text = "🗑️" } };
            deleteItem.Click += (s, args) => DeleteNode(node);
            menu.Items.Add(deleteItem);
            
            border.ContextMenu = menu;
            menu.IsOpen = true;
        }
    }

    /// <summary>
    /// Select node and show properties
    /// </summary>
    private void SelectNode(FlowNode node)
    {
        _selectedNode = node;
        _propertyGrid.SelectedObject = node;
        
        // Highlight selected node
        foreach (UIElement element in _canvas.Children)
        {
            if (element is Border border)
            {
                border.BorderBrush = border.Tag == node ? Brushes.Yellow : Brushes.White;
                border.BorderThickness = new Thickness(border.Tag == node ? 3 : 2);
            }
        }
    }

    /// <summary>
    /// Edit node configuration
    /// </summary>
    private void EditNode(FlowNode node)
    {
        var wizard = new FlowItemWizard(new ToolboxItem(node.Name, node.Type, node.Icon, ""), node.Configuration);
        if (wizard.ShowDialog() == true)
        {
            node.Configuration = wizard.Configuration;
            RefreshCanvas();
            UpdateRule();
        }
    }

    /// <summary>
    /// Duplicate node
    /// </summary>
    private void DuplicateNode(FlowNode node)
    {
        var newNode = new FlowNode
        {
            Id = Guid.NewGuid().ToString(),
            Type = node.Type,
            Name = node.Name + " (コピー)",
            Icon = node.Icon,
            Category = node.Category,
            Configuration = new Dictionary<string, object>(node.Configuration),
            Position = new System.Windows.Point(node.Position.X + 20, node.Position.Y + 20)
        };
        
        AddNodeToCanvas(newNode);
    }

    /// <summary>
    /// Delete node
    /// </summary>
    private void DeleteNode(FlowNode node)
    {
        var result = MessageBox.Show(
            $"'{node.Name}' を削除しますか？",
            "確認",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        
        if (result == MessageBoxResult.Yes)
        {
            _nodes.Remove(node);
            
            // Remove visual
            var toRemove = _canvas.Children.Cast<UIElement>()
                .FirstOrDefault(e => e is Border b && b.Tag == node);
            if (toRemove != null)
            {
                _canvas.Children.Remove(toRemove);
            }
            
            // Remove connections
            _connections.RemoveAll(c => c.SourceId == node.Id || c.TargetId == node.Id);
            
            RefreshCanvas();
            UpdateRule();
        }
    }

    /// <summary>
    /// Update rule from visual nodes
    /// </summary>
    private void UpdateRule()
    {
        CurrentRule = new AutomationDsl.Rule
        {
            Id = CurrentRule?.Id ?? Guid.NewGuid().ToString(),
            Name = CurrentRule?.Name ?? "New Flow",
            Description = CurrentRule?.Description ?? "",
            Enabled = true,
            Trigger = BuildTrigger(),
            Conditions = BuildConditions(),
            Actions = BuildActions(),
            Permissions = CalculatePermissions()
        };
    }

    /// <summary>
    /// Build trigger from nodes
    /// </summary>
    private AutomationDsl.TriggerDefinition BuildTrigger()
    {
        var triggerNode = _nodes.FirstOrDefault(n => n.Category == "trigger");
        if (triggerNode == null)
            return null;
        
        return new AutomationDsl.TriggerDefinition
        {
            Type = triggerNode.Type,
            Parameters = triggerNode.Configuration ?? new Dictionary<string, object>()
        };
    }

    /// <summary>
    /// Build conditions from nodes
    /// </summary>
    private List<AutomationDsl.ConditionDefinition> BuildConditions()
    {
        return _nodes
            .Where(n => n.Category == "condition")
            .Select(n => new AutomationDsl.ConditionDefinition
            {
                Type = n.Type,
                Parameters = n.Configuration ?? new Dictionary<string, object>()
            })
            .ToList();
    }

    /// <summary>
    /// Build actions from nodes
    /// </summary>
    private List<AutomationDsl.ActionDefinition> BuildActions()
    {
        return _nodes
            .Where(n => n.Category == "action")
            .OrderBy(n => n.Position.Y) // Order by vertical position
            .Select(n => new AutomationDsl.ActionDefinition
            {
                Type = n.Type,
                Parameters = n.Configuration ?? new Dictionary<string, object>()
            })
            .ToList();
    }

    /// <summary>
    /// Calculate required permissions
    /// </summary>
    private AutomationDsl.PermissionSet CalculatePermissions()
    {
        var permissions = new AutomationDsl.PermissionSet();
        
        foreach (var node in _nodes)
        {
            switch (node.Type)
            {
                case "httpRequest":
                case "webhook":
                    permissions.Network = true;
                    break;
                case "file":
                case "fileSystem":
                    permissions.FileSystem = true;
                    break;
                case "llmQuery":
                    permissions.Llm = true;
                    break;
            }
        }
        
        return permissions;
    }

    /// <summary>
    /// Refresh canvas display
    /// </summary>
    private void RefreshCanvas()
    {
        // Re-render all nodes
        _canvas.Children.Clear();
        
        // Draw connections first
        foreach (var connection in _connections)
        {
            DrawConnection(connection);
        }
        
        // Draw nodes
        foreach (var node in _nodes)
        {
            var visual = CreateNodeVisual(node);
            Canvas.SetLeft(visual, node.Position.X);
            Canvas.SetTop(visual, node.Position.Y);
            _canvas.Children.Add(visual);
        }
    }

    /// <summary>
    /// Draw connection between nodes
    /// </summary>
    private void DrawConnection(FlowConnection connection)
    {
        var source = _nodes.FirstOrDefault(n => n.Id == connection.SourceId);
        var target = _nodes.FirstOrDefault(n => n.Id == connection.TargetId);
        
        if (source == null || target == null)
            return;
        
        var line = new System.Windows.Shapes.Path
        {
            Stroke = Brushes.LightGray,
            StrokeThickness = 2,
            Data = new LineGeometry(
                new System.Windows.Point(source.Position.X + 90, source.Position.Y + 80),
                new System.Windows.Point(target.Position.X + 90, target.Position.Y))
        };
        
        _canvas.Children.Insert(0, line); // Add behind nodes
    }

    /// <summary>
    /// Handle drag operations
    /// </summary>
    private void Canvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_isDragging && _selectedNode != null && e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
        {
            var currentPoint = e.GetPosition(_canvas);
            var offsetX = currentPoint.X - _dragStartPoint.X;
            var offsetY = currentPoint.Y - _dragStartPoint.Y;
            
            _selectedNode.Position = new System.Windows.Point(
                _selectedNode.Position.X + offsetX,
                _selectedNode.Position.Y + offsetY);
            
            _dragStartPoint = currentPoint;
            RefreshCanvas();
        }
    }

    private void Canvas_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _isDragging = false;
        Mouse.Capture(null);
    }

    private void Canvas_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        // Deselect if clicking on empty canvas
        if (e.OriginalSource == _canvas)
        {
            SelectNode(null);
        }
    }

    private void Canvas_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        e.Effects = System.Windows.DragDropEffects.Copy;
        e.Handled = true;
    }

    private void Canvas_Drop(object sender, System.Windows.DragEventArgs e)
    {
        // Handle drop from toolbox
        if (e.Data.GetDataPresent(typeof(ToolboxItem)))
        {
            var item = (ToolboxItem)e.Data.GetData(typeof(ToolboxItem));
            var position = e.GetPosition(_canvas);
            
            var node = CreateNode(item, new Dictionary<string, object>());
            node.Position = position;
            
            AddNodeToCanvas(node);
        }
    }

    private void ToolboxItem_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
        {
            if (sender is Button button && button.Tag is ToolboxItem item)
            {
                DragDrop.DoDragDrop(button, item, System.Windows.DragDropEffects.Copy);
            }
        }
    }

    private string GetCategoryFromItem(ToolboxItem item)
    {
        // Determine category from item type
        if (item.Type.Contains("trigger") || item.Type == "time" || item.Type == "fileSystem" || 
            item.Type == "webhook" || item.Type == "application" || item.Type == "systemEvent")
            return "trigger";
        
        if (item.Type.Contains("condition") || item.Type == "timeRange" || item.Type == "variable")
            return "condition";
        
        if (item.Type == "if" || item.Type == "loop" || item.Type == "parallel")
            return "control";
        
        return "action";
    }
}

/// <summary>
/// Flow node representation
/// </summary>
public class FlowNode
{
    public string Id { get; set; }
    public string Type { get; set; }
    public string Name { get; set; }
    public string Icon { get; set; }
    public string Category { get; set; }
    public System.Windows.Point Position { get; set; }
    public Dictionary<string, object> Configuration { get; set; }
    public List<string> InputConnections { get; set; } = new();
    public List<string> OutputConnections { get; set; } = new();
}

/// <summary>
/// Connection between nodes
/// </summary>
public class FlowConnection
{
    public string Id { get; set; }
    public string SourceId { get; set; }
    public string TargetId { get; set; }
    public string Label { get; set; }
}

/// <summary>
/// Toolbox item
/// </summary>
public class ToolboxItem
{
    public string Name { get; set; }
    public string Type { get; set; }
    public string Icon { get; set; }
    public string Description { get; set; }
    
    public ToolboxItem(string name, string type, string icon, string description)
    {
        Name = name;
        Type = type;
        Icon = icon;
        Description = description;
    }
}

/// <summary>
/// Property grid for node configuration
/// </summary>
public class PropertyGrid : UserControl
{
    private StackPanel _panel;
    private object _selectedObject;
    
    public PropertyGrid()
    {
        _panel = new StackPanel
        {
            Background = new SolidColorBrush(Color.FromRgb(45, 45, 48)),
            Margin = new Thickness(10)
        };
        
        var scroll = new ScrollViewer { Content = _panel };
        Content = scroll;
    }
    
    public object SelectedObject
    {
        get => _selectedObject;
        set
        {
            _selectedObject = value;
            UpdateDisplay();
        }
    }
    
    private void UpdateDisplay()
    {
        _panel.Children.Clear();
        
        if (_selectedObject is FlowNode node)
        {
            AddHeader($"{node.Icon} {node.Name}");
            AddProperty("ID", node.Id, true);
            AddProperty("タイプ", node.Type, true);
            AddProperty("カテゴリ", node.Category, true);
            
            if (node.Configuration != null)
            {
                AddSeparator();
                AddHeader("設定");
                
                foreach (var kvp in node.Configuration)
                {
                    AddProperty(kvp.Key, kvp.Value?.ToString(), false);
                }
            }
        }
    }
    
    private void AddHeader(string text)
    {
        _panel.Children.Add(new TextBlock
        {
            Text = text,
            Foreground = Brushes.White,
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 10, 0, 5)
        });
    }
    
    private void AddProperty(string name, string value, bool isReadOnly)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        
        var nameLabel = new TextBlock
        {
            Text = name,
            Foreground = Brushes.LightGray,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(nameLabel, 0);
        
        var valueControl = isReadOnly
            ? (UIElement)new TextBlock
            {
                Text = value,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            }
            : new TextBox
            {
                Text = value,
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                Foreground = Brushes.White,
                BorderBrush = Brushes.Gray
            };
        Grid.SetColumn(valueControl, 1);
        
        grid.Children.Add(nameLabel);
        grid.Children.Add(valueControl);
        
        _panel.Children.Add(grid);
    }
    
    private void AddSeparator()
    {
        _panel.Children.Add(new Separator
        {
            Background = Brushes.Gray,
            Margin = new Thickness(0, 10, 0, 10)
        });
    }
}