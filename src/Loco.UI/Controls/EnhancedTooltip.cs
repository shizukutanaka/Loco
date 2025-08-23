using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Loco.UI.Controls
{
    /// <summary>
    /// Enhanced tooltip system with contextual help
    /// Provides rich, interactive tooltips with multimedia support
    /// </summary>
    public class EnhancedTooltip : ToolTip
    {
        private readonly DispatcherTimer _showTimer;
        private readonly DispatcherTimer _hideTimer;
        private bool _isMouseOver;

        public EnhancedTooltip()
        {
            _showTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _showTimer.Tick += ShowTimer_Tick;
            
            _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _hideTimer.Tick += HideTimer_Tick;

            InitializeStyle();
        }

        private void InitializeStyle()
        {
            Background = new SolidColorBrush(Color.FromRgb(45, 45, 48));
            Foreground = Brushes.White;
            BorderBrush = new SolidColorBrush(Color.FromRgb(0, 122, 204));
            BorderThickness = new Thickness(1);
            Padding = new Thickness(10);
            
            // Add shadow effect
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Colors.Black,
                Direction = 320,
                ShadowDepth = 2,
                Opacity = 0.5,
                BlurRadius = 5
            };

            // Add animation
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
            BeginAnimation(OpacityProperty, fadeIn);
        }

        private void ShowTimer_Tick(object sender, EventArgs e)
        {
            _showTimer.Stop();
            if (_isMouseOver)
            {
                IsOpen = true;
                _hideTimer.Start();
            }
        }

        private void HideTimer_Tick(object sender, EventArgs e)
        {
            _hideTimer.Stop();
            if (!_isMouseOver)
            {
                IsOpen = false;
            }
        }

        public void Show(UIElement placementTarget)
        {
            PlacementTarget = placementTarget;
            _isMouseOver = true;
            _showTimer.Start();
        }

        public void Hide()
        {
            _isMouseOver = false;
            _showTimer.Stop();
            IsOpen = false;
        }

        /// <summary>
        /// Create a rich tooltip with title and description
        /// </summary>
        public static EnhancedTooltip CreateRichTooltip(string title, string description, string helpKey = null)
        {
            var tooltip = new EnhancedTooltip();
            
            var panel = new StackPanel();
            
            // Title
            if (!string.IsNullOrEmpty(title))
            {
                panel.Children.Add(new TextBlock
                {
                    Text = title,
                    FontWeight = FontWeights.Bold,
                    FontSize = 14,
                    Margin = new Thickness(0, 0, 0, 5)
                });
            }
            
            // Description
            if (!string.IsNullOrEmpty(description))
            {
                panel.Children.Add(new TextBlock
                {
                    Text = description,
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 300
                });
            }
            
            // Help key hint
            if (!string.IsNullOrEmpty(helpKey))
            {
                panel.Children.Add(new TextBlock
                {
                    Text = $"Press {helpKey} for more help",
                    FontStyle = FontStyles.Italic,
                    FontSize = 10,
                    Margin = new Thickness(0, 5, 0, 0),
                    Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150))
                });
            }
            
            tooltip.Content = panel;
            return tooltip;
        }
    }

    /// <summary>
    /// Contextual help provider
    /// </summary>
    public static class ContextualHelp
    {
        private static readonly Dictionary<string, HelpContent> _helpDatabase;
        private static HelpWindow _currentHelpWindow;

        static ContextualHelp()
        {
            _helpDatabase = new Dictionary<string, HelpContent>();
            InitializeHelpDatabase();
        }

        private static void InitializeHelpDatabase()
        {
            // Initialize with default help content
            RegisterHelp("automation_rule", new HelpContent
            {
                Title = "Automation Rules",
                ShortDescription = "Create automated workflows triggered by events",
                DetailedDescription = "Automation rules allow you to create powerful workflows that respond to system events, " +
                                    "user actions, or scheduled times. Each rule consists of triggers, conditions, and actions.",
                Examples = new[]
                {
                    "When a file is created in a folder, copy it to backup",
                    "Every day at 9 AM, send a summary email",
                    "When CPU usage exceeds 80%, send an alert"
                },
                RelatedTopics = new[] { "triggers", "conditions", "actions" }
            });

            RegisterHelp("triggers", new HelpContent
            {
                Title = "Triggers",
                ShortDescription = "Events that start an automation",
                DetailedDescription = "Triggers are events that initiate an automation rule. They can be system events, " +
                                    "user actions, scheduled times, or external signals.",
                Examples = new[]
                {
                    "File system changes (create, modify, delete)",
                    "Time-based triggers (cron expressions)",
                    "Application events",
                    "HTTP webhooks"
                }
            });

            RegisterHelp("conditions", new HelpContent
            {
                Title = "Conditions",
                ShortDescription = "Filters that control when actions execute",
                DetailedDescription = "Conditions are optional filters that determine whether the actions should be executed " +
                                    "after a trigger fires. They allow for more precise control over automation behavior.",
                Examples = new[]
                {
                    "File size greater than 1MB",
                    "Current time between 9 AM and 5 PM",
                    "Variable equals specific value"
                }
            });

            RegisterHelp("actions", new HelpContent
            {
                Title = "Actions",
                ShortDescription = "Tasks performed when conditions are met",
                DetailedDescription = "Actions are the tasks that are executed when a trigger fires and all conditions are met. " +
                                    "Multiple actions can be chained together to create complex workflows.",
                Examples = new[]
                {
                    "Send email notification",
                    "Copy or move files",
                    "Execute scripts or programs",
                    "Make HTTP requests"
                }
            });
        }

        /// <summary>
        /// Register help content for a topic
        /// </summary>
        public static void RegisterHelp(string key, HelpContent content)
        {
            _helpDatabase[key] = content;
        }

        /// <summary>
        /// Attach contextual help to a control
        /// </summary>
        public static void AttachHelp(FrameworkElement control, string helpKey, string quickTip = null)
        {
            if (control == null || string.IsNullOrEmpty(helpKey)) return;

            // Get help content
            if (!_helpDatabase.TryGetValue(helpKey, out var helpContent))
            {
                helpContent = new HelpContent
                {
                    Title = helpKey,
                    ShortDescription = quickTip ?? "No help available"
                };
            }

            // Create and attach enhanced tooltip
            var tooltip = EnhancedTooltip.CreateRichTooltip(
                helpContent.Title,
                quickTip ?? helpContent.ShortDescription,
                "F1");
            
            control.ToolTip = tooltip;

            // Add F1 key handler
            control.KeyDown += (s, e) =>
            {
                if (e.Key == Key.F1)
                {
                    ShowDetailedHelp(helpKey);
                    e.Handled = true;
                }
            };

            // Add context menu with help option
            AddHelpContextMenu(control, helpKey);
        }

        /// <summary>
        /// Show detailed help window
        /// </summary>
        public static void ShowDetailedHelp(string helpKey)
        {
            if (!_helpDatabase.TryGetValue(helpKey, out var helpContent))
            {
                helpContent = new HelpContent
                {
                    Title = "Help",
                    ShortDescription = "Help content not found",
                    DetailedDescription = $"No detailed help is available for '{helpKey}'"
                };
            }

            // Close existing help window if open
            _currentHelpWindow?.Close();

            // Create new help window
            _currentHelpWindow = new HelpWindow(helpContent);
            _currentHelpWindow.Closed += (s, e) => _currentHelpWindow = null;
            _currentHelpWindow.Show();
        }

        /// <summary>
        /// Show quick help popup
        /// </summary>
        public static void ShowQuickHelp(string helpKey, UIElement placementTarget)
        {
            if (!_helpDatabase.TryGetValue(helpKey, out var helpContent))
                return;

            var popup = new Popup
            {
                PlacementTarget = placementTarget,
                Placement = PlacementMode.Right,
                StaysOpen = false,
                AllowsTransparency = true
            };

            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(255, 255, 225)),
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(10),
                MaxWidth = 300
            };

            var content = new StackPanel();
            
            content.Children.Add(new TextBlock
            {
                Text = helpContent.Title,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 5)
            });

            content.Children.Add(new TextBlock
            {
                Text = helpContent.ShortDescription,
                TextWrapping = TextWrapping.Wrap
            });

            border.Child = content;
            popup.Child = border;
            popup.IsOpen = true;

            // Auto-close after 3 seconds
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            timer.Tick += (s, e) =>
            {
                popup.IsOpen = false;
                timer.Stop();
            };
            timer.Start();
        }

        private static void AddHelpContextMenu(FrameworkElement control, string helpKey)
        {
            var contextMenu = control.ContextMenu ?? new ContextMenu();
            
            // Add separator if menu has existing items
            if (contextMenu.Items.Count > 0)
            {
                contextMenu.Items.Add(new Separator());
            }

            // Add help menu item
            var helpMenuItem = new MenuItem
            {
                Header = "Help",
                Icon = new TextBlock { Text = "?", FontWeight = FontWeights.Bold }
            };

            helpMenuItem.Click += (s, e) => ShowDetailedHelp(helpKey);
            contextMenu.Items.Add(helpMenuItem);

            control.ContextMenu = contextMenu;
        }
    }

    /// <summary>
    /// Help content model
    /// </summary>
    public class HelpContent
    {
        public string Title { get; set; }
        public string ShortDescription { get; set; }
        public string DetailedDescription { get; set; }
        public string[] Examples { get; set; }
        public string[] RelatedTopics { get; set; }
        public string VideoUrl { get; set; }
        public Dictionary<string, string> AdditionalResources { get; set; }
    }

    /// <summary>
    /// Help window for displaying detailed help
    /// </summary>
    public class HelpWindow : Window
    {
        private readonly HelpContent _content;

        public HelpWindow(HelpContent content)
        {
            _content = content;
            InitializeWindow();
        }

        private void InitializeWindow()
        {
            Title = $"Help - {_content.Title}";
            Width = 600;
            Height = 400;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            
            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(20)
            };

            var panel = new StackPanel();

            // Title
            panel.Children.Add(new TextBlock
            {
                Text = _content.Title,
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 10)
            });

            // Short description
            panel.Children.Add(new TextBlock
            {
                Text = _content.ShortDescription,
                FontSize = 14,
                FontStyle = FontStyles.Italic,
                Margin = new Thickness(0, 0, 0, 15),
                TextWrapping = TextWrapping.Wrap
            });

            // Detailed description
            if (!string.IsNullOrEmpty(_content.DetailedDescription))
            {
                panel.Children.Add(new TextBlock
                {
                    Text = "Description",
                    FontSize = 16,
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 0, 0, 5)
                });

                panel.Children.Add(new TextBlock
                {
                    Text = _content.DetailedDescription,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 15)
                });
            }

            // Examples
            if (_content.Examples != null && _content.Examples.Length > 0)
            {
                panel.Children.Add(new TextBlock
                {
                    Text = "Examples",
                    FontSize = 16,
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 0, 0, 5)
                });

                foreach (var example in _content.Examples)
                {
                    var examplePanel = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Margin = new Thickness(10, 2, 0, 2)
                    };

                    examplePanel.Children.Add(new TextBlock
                    {
                        Text = "•",
                        Margin = new Thickness(0, 0, 5, 0)
                    });

                    examplePanel.Children.Add(new TextBlock
                    {
                        Text = example,
                        TextWrapping = TextWrapping.Wrap
                    });

                    panel.Children.Add(examplePanel);
                }

                panel.Children.Add(new TextBlock { Height = 15 });
            }

            // Related topics
            if (_content.RelatedTopics != null && _content.RelatedTopics.Length > 0)
            {
                panel.Children.Add(new TextBlock
                {
                    Text = "Related Topics",
                    FontSize = 16,
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 0, 0, 5)
                });

                var relatedPanel = new WrapPanel { Margin = new Thickness(0, 0, 0, 15) };
                
                foreach (var topic in _content.RelatedTopics)
                {
                    var link = new Button
                    {
                        Content = topic,
                        Margin = new Thickness(0, 0, 5, 5),
                        Padding = new Thickness(10, 5, 10, 5),
                        Cursor = Cursors.Hand
                    };

                    link.Click += (s, e) => ContextualHelp.ShowDetailedHelp(topic);
                    relatedPanel.Children.Add(link);
                }

                panel.Children.Add(relatedPanel);
            }

            // Close button
            var closeButton = new Button
            {
                Content = "Close",
                Width = 80,
                Height = 30,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 20, 0, 0)
            };

            closeButton.Click += (s, e) => Close();
            panel.Children.Add(closeButton);

            scrollViewer.Content = panel;
            Content = scrollViewer;
        }
    }
}
