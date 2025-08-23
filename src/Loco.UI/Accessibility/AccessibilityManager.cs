using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Extensions.Logging;

namespace Loco.UI.Accessibility
{
    /// <summary>
    /// Accessibility manager for improving UI accessibility
    /// Implements WCAG 2.1 guidelines and Windows accessibility features
    /// </summary>
    public sealed class AccessibilityManager
    {
        private readonly ILogger<AccessibilityManager> _logger;
        private AccessibilitySettings _settings;
        private readonly Dictionary<Type, IAccessibilityEnhancer> _enhancers;
        
        private static AccessibilityManager _instance;
        private static readonly object _lock = new object();

        private AccessibilityManager(ILogger<AccessibilityManager> logger = null)
        {
            _logger = logger;
            _settings = AccessibilitySettings.Default;
            _enhancers = new Dictionary<Type, IAccessibilityEnhancer>();
            
            InitializeEnhancers();
        }

        public static AccessibilityManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new AccessibilityManager();
                        }
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// Apply accessibility enhancements to a control
        /// </summary>
        public void EnhanceControl(FrameworkElement control)
        {
            if (control == null) return;

            try
            {
                // Set automation properties
                SetAutomationProperties(control);

                // Apply keyboard navigation
                ApplyKeyboardNavigation(control);

                // Apply visual enhancements
                ApplyVisualEnhancements(control);

                // Apply specific enhancer if available
                var controlType = control.GetType();
                if (_enhancers.TryGetValue(controlType, out var enhancer))
                {
                    enhancer.Enhance(control, _settings);
                }

                // Apply to children
                if (control is Panel panel)
                {
                    foreach (UIElement child in panel.Children)
                    {
                        if (child is FrameworkElement childElement)
                        {
                            EnhanceControl(childElement);
                        }
                    }
                }
                else if (control is ContentControl contentControl && 
                         contentControl.Content is FrameworkElement content)
                {
                    EnhanceControl(content);
                }

                _logger?.LogDebug("Applied accessibility enhancements to {ControlType}", controlType.Name);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to enhance control {ControlType}", control.GetType().Name);
            }
        }

        /// <summary>
        /// Apply accessibility enhancements to a window
        /// </summary>
        public void EnhanceWindow(Window window)
        {
            if (window == null) return;

            try
            {
                // Set window automation properties
                AutomationProperties.SetName(window, window.Title);
                AutomationProperties.SetAutomationId(window, $"Window_{window.GetType().Name}");

                // Apply high contrast support
                if (_settings.EnableHighContrast)
                {
                    ApplyHighContrast(window);
                }

                // Apply font scaling
                if (_settings.FontScaleFactor != 1.0)
                {
                    ApplyFontScaling(window, _settings.FontScaleFactor);
                }

                // Enhance all controls in the window
                if (window.Content is FrameworkElement content)
                {
                    EnhanceControl(content);
                }

                // Add keyboard help
                if (_settings.EnableKeyboardHelp)
                {
                    AddKeyboardHelp(window);
                }

                _logger?.LogInformation("Applied accessibility enhancements to window {WindowTitle}", window.Title);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to enhance window {WindowTitle}", window.Title);
            }
        }

        /// <summary>
        /// Update accessibility settings
        /// </summary>
        public void UpdateSettings(AccessibilitySettings settings)
        {
            _settings = settings ?? AccessibilitySettings.Default;
            
            // Apply new settings to all open windows
            foreach (Window window in Application.Current.Windows)
            {
                EnhanceWindow(window);
            }

            _logger?.LogInformation("Updated accessibility settings");
        }

        /// <summary>
        /// Get current accessibility settings
        /// </summary>
        public AccessibilitySettings GetSettings()
        {
            return _settings;
        }

        /// <summary>
        /// Announce a message to screen readers
        /// </summary>
        public void Announce(string message, AnnouncementPriority priority = AnnouncementPriority.Normal)
        {
            if (string.IsNullOrEmpty(message)) return;

            try
            {
                // Create a live region for the announcement
                var liveRegion = new TextBlock
                {
                    Text = message,
                    Visibility = Visibility.Collapsed
                };

                // Set live region properties based on priority
                switch (priority)
                {
                    case AnnouncementPriority.Important:
                        AutomationProperties.SetLiveSetting(liveRegion, AutomationLiveSetting.Assertive);
                        break;
                    case AnnouncementPriority.Normal:
                        AutomationProperties.SetLiveSetting(liveRegion, AutomationLiveSetting.Polite);
                        break;
                    case AnnouncementPriority.Low:
                        AutomationProperties.SetLiveSetting(liveRegion, AutomationLiveSetting.Off);
                        break;
                }

                // Add to current window
                if (Application.Current.MainWindow?.Content is Panel rootPanel)
                {
                    rootPanel.Children.Add(liveRegion);
                    
                    // Remove after announcement
                    var timer = new System.Windows.Threading.DispatcherTimer
                    {
                        Interval = TimeSpan.FromSeconds(2)
                    };
                    timer.Tick += (s, e) =>
                    {
                        rootPanel.Children.Remove(liveRegion);
                        timer.Stop();
                    };
                    timer.Start();
                }

                _logger?.LogDebug("Announced message: {Message}", message);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to announce message");
            }
        }

        /// <summary>
        /// Check color contrast ratio
        /// </summary>
        public double CheckColorContrast(Color foreground, Color background)
        {
            // Calculate relative luminance
            double GetRelativeLuminance(Color color)
            {
                double r = color.R / 255.0;
                double g = color.G / 255.0;
                double b = color.B / 255.0;

                r = r <= 0.03928 ? r / 12.92 : Math.Pow((r + 0.055) / 1.055, 2.4);
                g = g <= 0.03928 ? g / 12.92 : Math.Pow((g + 0.055) / 1.055, 2.4);
                b = b <= 0.03928 ? b / 12.92 : Math.Pow((b + 0.055) / 1.055, 2.4);

                return 0.2126 * r + 0.7152 * g + 0.0722 * b;
            }

            var l1 = GetRelativeLuminance(foreground);
            var l2 = GetRelativeLuminance(background);

            var lighter = Math.Max(l1, l2);
            var darker = Math.Min(l1, l2);

            return (lighter + 0.05) / (darker + 0.05);
        }

        /// <summary>
        /// Validate control accessibility
        /// </summary>
        public AccessibilityValidationResult ValidateControl(FrameworkElement control)
        {
            var result = new AccessibilityValidationResult
            {
                ControlType = control.GetType().Name
            };

            var issues = new List<AccessibilityIssue>();

            // Check for name/label
            var name = AutomationProperties.GetName(control);
            var labeledBy = AutomationProperties.GetLabeledBy(control);
            
            if (string.IsNullOrEmpty(name) && labeledBy == null)
            {
                if (control is Button || control is TextBox || control is ComboBox)
                {
                    issues.Add(new AccessibilityIssue
                    {
                        Severity = IssueSeverity.Error,
                        Type = IssueType.MissingLabel,
                        Message = "Control is missing an accessible name or label"
                    });
                }
            }

            // Check keyboard accessibility
            if (control.Focusable && !control.IsTabStop)
            {
                issues.Add(new AccessibilityIssue
                {
                    Severity = IssueSeverity.Warning,
                    Type = IssueType.KeyboardAccess,
                    Message = "Focusable control is not in tab order"
                });
            }

            // Check color contrast for text elements
            if (control is TextBlock textBlock)
            {
                var foreground = textBlock.Foreground as SolidColorBrush;
                var background = GetBackgroundColor(textBlock);
                
                if (foreground != null && background != null)
                {
                    var contrast = CheckColorContrast(foreground.Color, background.Color);
                    if (contrast < 4.5) // WCAG AA standard
                    {
                        issues.Add(new AccessibilityIssue
                        {
                            Severity = IssueSeverity.Warning,
                            Type = IssueType.ColorContrast,
                            Message = $"Text color contrast ratio {contrast:F2} is below WCAG AA standard (4.5:1)"
                        });
                    }
                }
            }

            result.Issues = issues;
            result.IsAccessible = issues.Count == 0 || !issues.Any(i => i.Severity == IssueSeverity.Error);

            return result;
        }

        // Private methods
        private void InitializeEnhancers()
        {
            _enhancers[typeof(Button)] = new ButtonEnhancer();
            _enhancers[typeof(TextBox)] = new TextBoxEnhancer();
            _enhancers[typeof(ComboBox)] = new ComboBoxEnhancer();
            _enhancers[typeof(ListBox)] = new ListBoxEnhancer();
            _enhancers[typeof(DataGrid)] = new DataGridEnhancer();
        }

        private void SetAutomationProperties(FrameworkElement control)
        {
            // Set automation ID if not set
            if (string.IsNullOrEmpty(AutomationProperties.GetAutomationId(control)))
            {
                AutomationProperties.SetAutomationId(control, $"{control.GetType().Name}_{Guid.NewGuid():N}".Substring(0, 32));
            }

            // Set help text for common controls
            if (control is Button button && string.IsNullOrEmpty(AutomationProperties.GetHelpText(button)))
            {
                if (button.ToolTip is string tooltip)
                {
                    AutomationProperties.SetHelpText(button, tooltip);
                }
            }
        }

        private void ApplyKeyboardNavigation(FrameworkElement control)
        {
            if (!_settings.EnableKeyboardNavigation) return;

            // Ensure focusable elements are in tab order
            if (control is Button || control is TextBox || control is ComboBox || control is CheckBox || control is RadioButton)
            {
                if (!control.IsTabStop)
                {
                    control.IsTabStop = true;
                }
            }

            // Add keyboard shortcuts hints
            if (_settings.ShowKeyboardHints && control is Button btn)
            {
                var gesture = GetKeyGesture(btn);
                if (gesture != null)
                {
                    AutomationProperties.SetAcceleratorKey(btn, gesture.DisplayString);
                }
            }
        }

        private void ApplyVisualEnhancements(FrameworkElement control)
        {
            // Apply focus indicators
            if (_settings.EnhancedFocusIndicators)
            {
                control.GotFocus += (s, e) =>
                {
                    if (s is Control c)
                    {
                        c.BorderThickness = new Thickness(2);
                        c.BorderBrush = new SolidColorBrush(Colors.Blue);
                    }
                };

                control.LostFocus += (s, e) =>
                {
                    if (s is Control c)
                    {
                        c.BorderThickness = new Thickness(1);
                        c.BorderBrush = new SolidColorBrush(Colors.Gray);
                    }
                };
            }
        }

        private void ApplyHighContrast(Window window)
        {
            // Apply high contrast theme
            var highContrastTheme = new ResourceDictionary
            {
                Source = new Uri("/Loco.UI;component/Themes/HighContrastTheme.xaml", UriKind.Relative)
            };
            
            window.Resources.MergedDictionaries.Add(highContrastTheme);
        }

        private void ApplyFontScaling(DependencyObject element, double scaleFactor)
        {
            // Recursively apply font scaling
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
            {
                var child = VisualTreeHelper.GetChild(element, i);
                
                if (child is Control control)
                {
                    control.FontSize = control.FontSize * scaleFactor;
                }
                else if (child is TextBlock textBlock)
                {
                    textBlock.FontSize = textBlock.FontSize * scaleFactor;
                }

                ApplyFontScaling(child, scaleFactor);
            }
        }

        private void AddKeyboardHelp(Window window)
        {
            // Add F1 key handler for help
            window.KeyDown += (s, e) =>
            {
                if (e.Key == Key.F1)
                {
                    ShowKeyboardHelp();
                }
            };
        }

        private void ShowKeyboardHelp()
        {
            Announce("Press F1 for help, Tab to navigate, Enter to activate", AnnouncementPriority.Important);
        }

        private KeyGesture GetKeyGesture(Button button)
        {
            // Check for input bindings
            foreach (InputBinding binding in button.InputBindings)
            {
                if (binding is KeyBinding keyBinding)
                {
                    return keyBinding.Gesture as KeyGesture;
                }
            }
            return null;
        }

        private Color GetBackgroundColor(FrameworkElement element)
        {
            // Traverse up the visual tree to find background color
            DependencyObject current = element;
            while (current != null)
            {
                if (current is Control control && control.Background is SolidColorBrush brush)
                {
                    return brush.Color;
                }
                current = VisualTreeHelper.GetParent(current);
            }
            return Colors.White; // Default
        }
    }

    // Supporting classes and interfaces
    public interface IAccessibilityEnhancer
    {
        void Enhance(FrameworkElement control, AccessibilitySettings settings);
    }

    public class ButtonEnhancer : IAccessibilityEnhancer
    {
        public void Enhance(FrameworkElement control, AccessibilitySettings settings)
        {
            if (control is Button button)
            {
                // Ensure minimum size for touch targets
                if (settings.EnsureMinimumTouchTargets)
                {
                    button.MinHeight = 44;
                    button.MinWidth = 44;
                }

                // Add role description
                AutomationProperties.SetItemType(button, "Button");
            }
        }
    }

    public class TextBoxEnhancer : IAccessibilityEnhancer
    {
        public void Enhance(FrameworkElement control, AccessibilitySettings settings)
        {
            if (control is TextBox textBox)
            {
                // Add placeholder as help text if no label
                if (string.IsNullOrEmpty(AutomationProperties.GetName(textBox)))
                {
                    var placeholder = textBox.Tag as string;
                    if (!string.IsNullOrEmpty(placeholder))
                    {
                        AutomationProperties.SetHelpText(textBox, placeholder);
                    }
                }

                // Set live region for validation messages
                AutomationProperties.SetLiveSetting(textBox, AutomationLiveSetting.Polite);
            }
        }
    }

    public class ComboBoxEnhancer : IAccessibilityEnhancer
    {
        public void Enhance(FrameworkElement control, AccessibilitySettings settings)
        {
            if (control is ComboBox comboBox)
            {
                // Set expanded/collapsed state
                comboBox.DropDownOpened += (s, e) =>
                {
                    AutomationProperties.SetItemStatus(comboBox, "Expanded");
                };

                comboBox.DropDownClosed += (s, e) =>
                {
                    AutomationProperties.SetItemStatus(comboBox, "Collapsed");
                };
            }
        }
    }

    public class ListBoxEnhancer : IAccessibilityEnhancer
    {
        public void Enhance(FrameworkElement control, AccessibilitySettings settings)
        {
            if (control is ListBox listBox)
            {
                // Add item count information
                var itemCount = listBox.Items.Count;
                AutomationProperties.SetHelpText(listBox, $"List with {itemCount} items");

                // Update on selection change
                listBox.SelectionChanged += (s, e) =>
                {
                    if (listBox.SelectedItem != null)
                    {
                        var index = listBox.SelectedIndex + 1;
                        AccessibilityManager.Instance.Announce($"Item {index} of {listBox.Items.Count} selected");
                    }
                };
            }
        }
    }

    public class DataGridEnhancer : IAccessibilityEnhancer
    {
        public void Enhance(FrameworkElement control, AccessibilitySettings settings)
        {
            if (control is DataGrid dataGrid)
            {
                // Set table role
                AutomationProperties.SetItemType(dataGrid, "Table");

                // Add row/column information
                dataGrid.CurrentCellChanged += (s, e) =>
                {
                    if (dataGrid.CurrentCell.Column != null)
                    {
                        var row = dataGrid.Items.IndexOf(dataGrid.CurrentCell.Item) + 1;
                        var col = dataGrid.CurrentCell.Column.DisplayIndex + 1;
                        AccessibilityManager.Instance.Announce($"Row {row}, Column {col}");
                    }
                };
            }
        }
    }

    public class AccessibilitySettings
    {
        public bool EnableKeyboardNavigation { get; set; } = true;
        public bool EnableHighContrast { get; set; } = false;
        public bool ShowKeyboardHints { get; set; } = true;
        public bool EnhancedFocusIndicators { get; set; } = true;
        public bool EnsureMinimumTouchTargets { get; set; } = true;
        public bool EnableKeyboardHelp { get; set; } = true;
        public double FontScaleFactor { get; set; } = 1.0;
        public bool AnnounceStatusChanges { get; set; } = true;
        public bool SimplifiedUI { get; set; } = false;
        public int AnimationSpeed { get; set; } = 100; // 0-100, 0 = no animations

        public static AccessibilitySettings Default => new AccessibilitySettings();

        public static AccessibilitySettings HighAccessibility => new AccessibilitySettings
        {
            EnableHighContrast = true,
            FontScaleFactor = 1.2,
            EnhancedFocusIndicators = true,
            AnimationSpeed = 0
        };
    }

    public enum AnnouncementPriority
    {
        Low,
        Normal,
        Important
    }

    public class AccessibilityValidationResult
    {
        public string ControlType { get; set; }
        public bool IsAccessible { get; set; }
        public List<AccessibilityIssue> Issues { get; set; }
    }

    public class AccessibilityIssue
    {
        public IssueSeverity Severity { get; set; }
        public IssueType Type { get; set; }
        public string Message { get; set; }
    }

    public enum IssueSeverity
    {
        Info,
        Warning,
        Error
    }

    public enum IssueType
    {
        MissingLabel,
        KeyboardAccess,
        ColorContrast,
        FocusOrder,
        MissingRole,
        InvalidStructure
    }
}
