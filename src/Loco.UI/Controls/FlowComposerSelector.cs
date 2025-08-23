using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Collections.Generic;
using Loco.Core.FlowComposer;
using Microsoft.Extensions.Logging.Abstractions;

namespace Loco.UI.Controls;

/// <summary>
/// Flow Composer visual component selector control
/// Visual selection with icons and categories
/// </summary>
public class FlowComposerSelector : UserControl
{
    private readonly FlowComposerBuilder _builder;
    private readonly Grid _mainGrid;
    private readonly ListView _categoryList;
    private readonly ListView _componentList;
    private readonly StackPanel _parameterPanel;
    private ComponentCategory _selectedCategory;
    private ComponentDefinition _selectedComponent;
    
    public ObservableCollection<ComponentCategory> Categories { get; }
    public ObservableCollection<ComponentDefinition> Components { get; }
    
    // Events
    public event EventHandler<ComponentSelectedEventArgs> ComponentSelected;
    public event EventHandler<ComponentConfiguredEventArgs> ComponentConfigured;
    
    public FlowComposerSelector()
    {
        _builder = new FlowComposerBuilder(NullLogger<FlowComposerBuilder>.Instance);
        Categories = new ObservableCollection<ComponentCategory>(_builder.GetCategories());
        Components = new ObservableCollection<ComponentDefinition>();
        
        InitializeUI();
    }
    
    private void InitializeUI()
    {
        // Main grid with 3 columns
        _mainGrid = new Grid();
        _mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
        _mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) });
        _mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        
        // Category list (left panel)
        _categoryList = CreateCategoryList();
        Grid.SetColumn(_categoryList, 0);
        _mainGrid.Children.Add(_categoryList);
        
        // Component list (middle panel)
        _componentList = CreateComponentList();
        Grid.SetColumn(_componentList, 1);
        _mainGrid.Children.Add(_componentList);
        
        // Parameter panel (right panel)
        var scrollViewer = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        _parameterPanel = new StackPanel
        {
            Margin = new Thickness(10)
        };
        scrollViewer.Content = _parameterPanel;
        Grid.SetColumn(scrollViewer, 2);
        _mainGrid.Children.Add(scrollViewer);
        
        this.Content = _mainGrid;
    }
    
    private ListView CreateCategoryList()
    {
        var listView = new ListView
        {
            ItemsSource = Categories,
            Margin = new Thickness(5),
            Background = new SolidColorBrush(Color.FromRgb(245, 245, 245))
        };
        
        // Item template
        var template = new DataTemplate();
        var factory = new FrameworkElementFactory(typeof(Border));
        factory.SetValue(Border.PaddingProperty, new Thickness(10));
        factory.SetValue(Border.MarginProperty, new Thickness(2));
        factory.SetValue(Border.CornerRadiusProperty, new CornerRadius(5));
        factory.SetValue(Border.BackgroundProperty, Brushes.White);
        
        var stackPanel = new FrameworkElementFactory(typeof(StackPanel));
        stackPanel.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
        
        var iconText = new FrameworkElementFactory(typeof(TextBlock));
        iconText.SetBinding(TextBlock.TextProperty, new Binding("Icon"));
        iconText.SetValue(TextBlock.FontSizeProperty, 24.0);
        iconText.SetValue(TextBlock.MarginProperty, new Thickness(0, 0, 10, 0));
        stackPanel.AppendChild(iconText);
        
        var textPanel = new FrameworkElementFactory(typeof(StackPanel));
        
        var nameText = new FrameworkElementFactory(typeof(TextBlock));
        nameText.SetBinding(TextBlock.TextProperty, new Binding("Name"));
        nameText.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
        nameText.SetValue(TextBlock.FontSizeProperty, 14.0);
        textPanel.AppendChild(nameText);
        
        var descText = new FrameworkElementFactory(typeof(TextBlock));
        descText.SetBinding(TextBlock.TextProperty, new Binding("Description"));
        descText.SetValue(TextBlock.ForegroundProperty, Brushes.Gray);
        descText.SetValue(TextBlock.FontSizeProperty, 10.0);
        textPanel.AppendChild(descText);
        
        stackPanel.AppendChild(textPanel);
        factory.AppendChild(stackPanel);
        
        template.VisualTree = factory;
        listView.ItemTemplate = template;
        
        // Selection changed event
        listView.SelectionChanged += OnCategorySelectionChanged;
        
        return listView;
    }
    
    private ListView CreateComponentList()
    {
        var listView = new ListView
        {
            ItemsSource = Components,
            Margin = new Thickness(5),
            Background = new SolidColorBrush(Color.FromRgb(250, 250, 250))
        };
        
        // Item template
        var template = new DataTemplate();
        var factory = new FrameworkElementFactory(typeof(Border));
        factory.SetValue(Border.PaddingProperty, new Thickness(8));
        factory.SetValue(Border.MarginProperty, new Thickness(2));
        factory.SetValue(Border.CornerRadiusProperty, new CornerRadius(5));
        factory.SetValue(Border.BackgroundProperty, Brushes.White);
        factory.SetValue(Border.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(200, 200, 200)));
        factory.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        
        var grid = new FrameworkElementFactory(typeof(Grid));
        grid.SetValue(Grid.ColumnDefinitionsProperty, CreateComponentGridColumns());
        
        var iconText = new FrameworkElementFactory(typeof(TextBlock));
        iconText.SetBinding(TextBlock.TextProperty, new Binding("Icon"));
        iconText.SetValue(TextBlock.FontSizeProperty, 20.0);
        iconText.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
        iconText.SetValue(Grid.ColumnProperty, 0);
        grid.AppendChild(iconText);
        
        var textPanel = new FrameworkElementFactory(typeof(StackPanel));
        textPanel.SetValue(Grid.ColumnProperty, 1);
        textPanel.SetValue(StackPanel.MarginProperty, new Thickness(10, 0, 0, 0));
        
        var nameText = new FrameworkElementFactory(typeof(TextBlock));
        nameText.SetBinding(TextBlock.TextProperty, new Binding("Name"));
        nameText.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
        nameText.SetValue(TextBlock.FontSizeProperty, 13.0);
        textPanel.AppendChild(nameText);
        
        var descText = new FrameworkElementFactory(typeof(TextBlock));
        descText.SetBinding(TextBlock.TextProperty, new Binding("Description"));
        descText.SetValue(TextBlock.ForegroundProperty, Brushes.DarkGray);
        descText.SetValue(TextBlock.FontSizeProperty, 11.0);
        descText.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
        textPanel.AppendChild(descText);
        
        grid.AppendChild(textPanel);
        factory.AppendChild(grid);
        
        template.VisualTree = factory;
        listView.ItemTemplate = template;
        
        // Selection changed event
        listView.SelectionChanged += OnComponentSelectionChanged;
        
        return listView;
    }
    
    private ColumnDefinitionCollection CreateComponentGridColumns()
    {
        var columns = new ColumnDefinitionCollection();
        columns.Add(new ColumnDefinition { Width = GridLength.Auto });
        columns.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        return columns;
    }
    
    private void OnCategorySelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_categoryList.SelectedItem is ComponentCategory category)
        {
            _selectedCategory = category;
            Components.Clear();
            foreach (var component in category.Components)
            {
                Components.Add(component);
            }
        }
    }
    
    private void OnComponentSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_componentList.SelectedItem is ComponentDefinition component)
        {
            _selectedComponent = component;
            ShowParameterEditor(component);
            
            // Raise event
            ComponentSelected?.Invoke(this, new ComponentSelectedEventArgs(component));
        }
    }
    
    private void ShowParameterEditor(ComponentDefinition component)
    {
        _parameterPanel.Children.Clear();
        
        // Title
        var titleText = new TextBlock
        {
            Text = component.Name,
            FontSize = 18,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 10)
        };
        _parameterPanel.Children.Add(titleText);
        
        // Description
        var descText = new TextBlock
        {
            Text = component.Description,
            FontSize = 12,
            Foreground = Brushes.Gray,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 15)
        };
        _parameterPanel.Children.Add(descText);
        
        // Parameters
        var paramValues = new System.Collections.Generic.Dictionary<string, object>();
        
        foreach (var param in component.Parameters)
        {
            // Parameter label
            var labelPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 5, 0, 3)
            };
            
            var label = new TextBlock
            {
                Text = param.DisplayName,
                FontWeight = FontWeights.SemiBold,
                FontSize = 12
            };
            labelPanel.Children.Add(label);
            
            if (param.Required)
            {
                var requiredMark = new TextBlock
                {
                    Text = " *",
                    Foreground = Brushes.Red,
                    FontSize = 12
                };
                labelPanel.Children.Add(requiredMark);
            }
            
            _parameterPanel.Children.Add(labelPanel);
            
            // Parameter input control
            Control inputControl = CreateParameterControl(param);
            inputControl.Tag = param.Name;
            inputControl.Margin = new Thickness(0, 0, 0, 10);
            _parameterPanel.Children.Add(inputControl);
            
            // Set default value
            if (param.Default != null)
            {
                SetControlValue(inputControl, param.Default);
                paramValues[param.Name] = param.Default;
            }
        }
        
        // Add button
        var addButton = new Button
        {
            Content = "コンポーネントを追加",
            Padding = new Thickness(15, 8, 15, 8),
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Background = new SolidColorBrush(Color.FromRgb(0, 123, 255)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Margin = new Thickness(0, 20, 0, 0),
            Cursor = System.Windows.Input.Cursors.Hand
        };
        
        addButton.Click += (s, e) =>
        {
            // Collect parameter values
            foreach (var child in _parameterPanel.Children)
            {
                if (child is Control control && control.Tag is string paramName)
                {
                    var value = GetControlValue(control);
                    if (value != null)
                    {
                        paramValues[paramName] = value;
                    }
                }
            }
            
            // Raise event
            ComponentConfigured?.Invoke(this, new ComponentConfiguredEventArgs(component, paramValues));
        };
        
        _parameterPanel.Children.Add(addButton);
    }
    
    private Control CreateParameterControl(ParameterDefinition param)
    {
        switch (param.Type.ToLower())
        {
            case "text":
            case "email":
            case "url":
                return new TextBox
                {
                    Height = 25,
                    Padding = new Thickness(5),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200))
                };
                
            case "textarea":
                return new TextBox
                {
                    Height = 80,
                    TextWrapping = TextWrapping.Wrap,
                    AcceptsReturn = true,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    Padding = new Thickness(5),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200))
                };
                
            case "number":
                var numberBox = new TextBox
                {
                    Height = 25,
                    Padding = new Thickness(5),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200))
                };
                numberBox.PreviewTextInput += (s, e) =>
                {
                    e.Handled = !int.TryParse(e.Text, out _);
                };
                return numberBox;
                
            case "boolean":
                return new CheckBox
                {
                    VerticalAlignment = VerticalAlignment.Center
                };
                
            case "select":
                return new ComboBox
                {
                    ItemsSource = param.Options,
                    Height = 25
                };
                
            case "multiselect":
                var listBox = new ListBox
                {
                    ItemsSource = param.Options,
                    SelectionMode = SelectionMode.Multiple,
                    Height = 100,
                    BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200))
                };
                return listBox;
                
            case "slider":
                var sliderPanel = new StackPanel();
                var slider = new Slider
                {
                    Minimum = Convert.ToDouble(param.Min ?? 0),
                    Maximum = Convert.ToDouble(param.Max ?? 100),
                    Value = Convert.ToDouble(param.Default ?? 50),
                    TickFrequency = 0.1,
                    IsSnapToTickEnabled = true
                };
                var valueLabel = new TextBlock
                {
                    Text = slider.Value.ToString("F1"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 5, 0, 0)
                };
                slider.ValueChanged += (s, e) => valueLabel.Text = e.NewValue.ToString("F1");
                sliderPanel.Children.Add(slider);
                sliderPanel.Children.Add(valueLabel);
                return sliderPanel;
                
            case "path":
                var pathPanel = new Grid();
                pathPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                pathPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                
                var pathBox = new TextBox
                {
                    Height = 25,
                    Padding = new Thickness(5),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200))
                };
                Grid.SetColumn(pathBox, 0);
                
                var browseButton = new Button
                {
                    Content = "参照...",
                    Margin = new Thickness(5, 0, 0, 0),
                    Padding = new Thickness(10, 3, 10, 3)
                };
                Grid.SetColumn(browseButton, 1);
                
                browseButton.Click += (s, e) =>
                {
                    var dialog = new Microsoft.Win32.OpenFileDialog();
                    if (dialog.ShowDialog() == true)
                    {
                        pathBox.Text = dialog.FileName;
                    }
                };
                
                pathPanel.Children.Add(pathBox);
                pathPanel.Children.Add(browseButton);
                return pathPanel;
                
            case "time":
                var timePanel = new StackPanel { Orientation = Orientation.Horizontal };
                var hourBox = new TextBox { Width = 30, Margin = new Thickness(0, 0, 5, 0) };
                var colonLabel = new TextBlock { Text = ":", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 5, 0) };
                var minuteBox = new TextBox { Width = 30 };
                timePanel.Children.Add(hourBox);
                timePanel.Children.Add(colonLabel);
                timePanel.Children.Add(minuteBox);
                return timePanel;
                
            case "keyvalue":
                var kvPanel = new StackPanel();
                var kvLabel = new TextBlock { Text = "キーと値のペア (JSON形式)", FontSize = 10, Foreground = Brushes.Gray };
                var kvBox = new TextBox
                {
                    Height = 60,
                    TextWrapping = TextWrapping.Wrap,
                    AcceptsReturn = true,
                    Padding = new Thickness(5),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                    Text = "{}"
                };
                kvPanel.Children.Add(kvLabel);
                kvPanel.Children.Add(kvBox);
                return kvPanel;
                
            case "variable":
                var varCombo = new ComboBox
                {
                    IsEditable = true,
                    Height = 25
                };
                // Populate with existing variables if available
                return varCombo;
                
            default:
                return new TextBox
                {
                    Height = 25,
                    Padding = new Thickness(5),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200))
                };
        }
    }
    
    private void SetControlValue(Control control, object value)
    {
        switch (control)
        {
            case TextBox textBox:
                textBox.Text = value?.ToString() ?? "";
                break;
            case CheckBox checkBox:
                checkBox.IsChecked = Convert.ToBoolean(value);
                break;
            case ComboBox comboBox:
                comboBox.SelectedItem = value;
                break;
            case ListBox listBox:
                if (value is System.Collections.IEnumerable items)
                {
                    foreach (var item in items)
                    {
                        listBox.SelectedItems.Add(item);
                    }
                }
                break;
            case StackPanel panel when panel.Children[0] is Slider slider:
                slider.Value = Convert.ToDouble(value);
                break;
        }
    }
    
    private object GetControlValue(Control control)
    {
        switch (control)
        {
            case TextBox textBox:
                return textBox.Text;
            case CheckBox checkBox:
                return checkBox.IsChecked ?? false;
            case ComboBox comboBox:
                return comboBox.SelectedItem;
            case ListBox listBox:
                return listBox.SelectedItems.Cast<object>().ToList();
            case StackPanel panel when panel.Children[0] is Slider slider:
                return slider.Value;
            case Grid grid when grid.Children[0] is TextBox pathBox:
                return pathBox.Text;
            case StackPanel timePanel when timePanel.Children.Count == 3:
                var hour = (timePanel.Children[0] as TextBox)?.Text;
                var minute = (timePanel.Children[2] as TextBox)?.Text;
                return $"{hour}:{minute}";
            default:
                return null;
        }
    }
}

/// <summary>
/// Component selected event args
/// </summary>
public class ComponentSelectedEventArgs : EventArgs
{
    public ComponentDefinition Component { get; }
    
    public ComponentSelectedEventArgs(ComponentDefinition component)
    {
        Component = component;
    }
}

/// <summary>
/// Component configured event args
/// </summary>
public class ComponentConfiguredEventArgs : EventArgs
{
    public ComponentDefinition Component { get; }
    public System.Collections.Generic.Dictionary<string, object> Parameters { get; }
    
    public ComponentConfiguredEventArgs(ComponentDefinition component, System.Collections.Generic.Dictionary<string, object> parameters)
    {
        Component = component;
        Parameters = parameters;
    }
}
