using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using Loco.Core.Models;
using Loco.Core.Services;
using Loco.Core.Performance;
using Loco.Core.Scheduling;

namespace Loco.UI.Controls
{
    /// <summary>
    /// Dashboard control for statistics and activity overview
    /// Provides real-time monitoring and analytics
    /// </summary>
    public class DashboardControl : UserControl
    {
        private readonly ILogger<DashboardControl> _logger;
        private readonly PerformanceMonitor _performanceMonitor;
        private readonly TaskSchedulingService _schedulingService;
        private readonly NotificationService _notificationService;
        private readonly DispatcherTimer _updateTimer;
        
        // UI Elements
        private Grid _mainGrid;
        private TextBlock _totalRulesText;
        private TextBlock _activeRulesText;
        private TextBlock _executionsText;
        private TextBlock _successRateText;
        private Canvas _performanceChart;
        private ListBox _recentActivityList;
        private StackPanel _scheduledTasksPanel;
        private ProgressBar _cpuProgressBar;
        private ProgressBar _memoryProgressBar;
        private TextBlock _cpuText;
        private TextBlock _memoryText;
        
        // Data
        private readonly List<double> _performanceHistory;
        private readonly Queue<ActivityItem> _recentActivities;
        private const int MaxHistoryPoints = 60;
        private const int MaxActivities = 20;

        public DashboardControl(
            ILogger<DashboardControl> logger = null,
            PerformanceMonitor performanceMonitor = null,
            TaskSchedulingService schedulingService = null,
            NotificationService notificationService = null)
        {
            _logger = logger;
            _performanceMonitor = performanceMonitor ?? PerformanceOptimizer.GlobalMonitor;
            _schedulingService = schedulingService;
            _notificationService = notificationService;
            
            _performanceHistory = new List<double>(MaxHistoryPoints);
            _recentActivities = new Queue<ActivityItem>(MaxActivities);
            
            InitializeUI();
            
            // Start update timer
            _updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _updateTimer.Tick += UpdateTimer_Tick;
            _updateTimer.Start();
        }

        private void InitializeUI()
        {
            _mainGrid = new Grid();
            
            // Define rows and columns
            _mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
            _mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(150) }); // Stats cards
            _mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(200) }); // Performance chart
            _mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Activities
            _mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // System stats
            
            _mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) }); // Side panel
            
            // Header
            var headerText = new TextBlock
            {
                Text = "Dashboard",
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(10)
            };
            Grid.SetRow(headerText, 0);
            Grid.SetColumnSpan(headerText, 2);
            _mainGrid.Children.Add(headerText);
            
            // Stats cards
            var statsPanel = CreateStatsPanel();
            Grid.SetRow(statsPanel, 1);
            Grid.SetColumn(statsPanel, 0);
            _mainGrid.Children.Add(statsPanel);
            
            // Performance chart
            var chartPanel = CreatePerformanceChart();
            Grid.SetRow(chartPanel, 2);
            Grid.SetColumn(chartPanel, 0);
            _mainGrid.Children.Add(chartPanel);
            
            // Recent activities
            var activitiesPanel = CreateActivitiesPanel();
            Grid.SetRow(activitiesPanel, 3);
            Grid.SetColumn(activitiesPanel, 0);
            _mainGrid.Children.Add(activitiesPanel);
            
            // Scheduled tasks (side panel)
            var scheduledPanel = CreateScheduledTasksPanel();
            Grid.SetRow(scheduledPanel, 1);
            Grid.SetRowSpan(scheduledPanel, 3);
            Grid.SetColumn(scheduledPanel, 1);
            _mainGrid.Children.Add(scheduledPanel);
            
            // System stats
            var systemPanel = CreateSystemStatsPanel();
            Grid.SetRow(systemPanel, 4);
            Grid.SetColumnSpan(systemPanel, 2);
            _mainGrid.Children.Add(systemPanel);
            
            Content = _mainGrid;
        }

        private StackPanel CreateStatsPanel()
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(10)
            };
            
            // Total Rules Card
            var totalRulesCard = CreateStatCard("Total Rules", "0", Colors.Blue);
            _totalRulesText = (TextBlock)((StackPanel)((Border)totalRulesCard).Child).Children[1];
            panel.Children.Add(totalRulesCard);
            
            // Active Rules Card
            var activeRulesCard = CreateStatCard("Active Rules", "0", Colors.Green);
            _activeRulesText = (TextBlock)((StackPanel)((Border)activeRulesCard).Child).Children[1];
            panel.Children.Add(activeRulesCard);
            
            // Executions Card
            var executionsCard = CreateStatCard("Executions", "0", Colors.Orange);
            _executionsText = (TextBlock)((StackPanel)((Border)executionsCard).Child).Children[1];
            panel.Children.Add(executionsCard);
            
            // Success Rate Card
            var successCard = CreateStatCard("Success Rate", "0%", Colors.Purple);
            _successRateText = (TextBlock)((StackPanel)((Border)successCard).Child).Children[1];
            panel.Children.Add(successCard);
            
            return panel;
        }

        private Border CreateStatCard(string title, string value, Color color)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(20, color.R, color.G, color.B)),
                BorderBrush = new SolidColorBrush(color),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(5),
                Margin = new Thickness(5),
                Padding = new Thickness(15),
                MinWidth = 120
            };
            
            var content = new StackPanel();
            
            content.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 12,
                Foreground = Brushes.Gray
            });
            
            content.Children.Add(new TextBlock
            {
                Text = value,
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(color)
            });
            
            border.Child = content;
            return border;
        }

        private Border CreatePerformanceChart()
        {
            var border = new Border
            {
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(10),
                Background = Brushes.White
            };
            
            _performanceChart = new Canvas
            {
                Background = Brushes.Transparent
            };
            
            border.Child = _performanceChart;
            return border;
        }

        private Border CreateActivitiesPanel()
        {
            var border = new Border
            {
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(10)
            };
            
            var panel = new DockPanel();
            
            var header = new TextBlock
            {
                Text = "Recent Activity",
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(5)
            };
            DockPanel.SetDock(header, Dock.Top);
            panel.Children.Add(header);
            
            _recentActivityList = new ListBox
            {
                BorderThickness = new Thickness(0),
                ScrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            panel.Children.Add(_recentActivityList);
            
            border.Child = panel;
            return border;
        }

        private Border CreateScheduledTasksPanel()
        {
            var border = new Border
            {
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(10)
            };
            
            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            
            var panel = new StackPanel();
            
            var header = new TextBlock
            {
                Text = "Scheduled Tasks",
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(5)
            };
            panel.Children.Add(header);
            
            _scheduledTasksPanel = new StackPanel
            {
                Margin = new Thickness(5)
            };
            panel.Children.Add(_scheduledTasksPanel);
            
            scrollViewer.Content = panel;
            border.Child = scrollViewer;
            return border;
        }

        private Grid CreateSystemStatsPanel()
        {
            var grid = new Grid
            {
                Margin = new Thickness(10)
            };
            
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            
            // CPU Usage
            var cpuPanel = new StackPanel { Margin = new Thickness(5) };
            cpuPanel.Children.Add(new TextBlock { Text = "CPU Usage", FontWeight = FontWeights.SemiBold });
            
            _cpuProgressBar = new ProgressBar
            {
                Height = 20,
                Minimum = 0,
                Maximum = 100,
                Foreground = Brushes.Green
            };
            cpuPanel.Children.Add(_cpuProgressBar);
            
            _cpuText = new TextBlock { Text = "0%", HorizontalAlignment = HorizontalAlignment.Center };
            cpuPanel.Children.Add(_cpuText);
            
            Grid.SetColumn(cpuPanel, 0);
            grid.Children.Add(cpuPanel);
            
            // Memory Usage
            var memoryPanel = new StackPanel { Margin = new Thickness(5) };
            memoryPanel.Children.Add(new TextBlock { Text = "Memory Usage", FontWeight = FontWeights.SemiBold });
            
            _memoryProgressBar = new ProgressBar
            {
                Height = 20,
                Minimum = 0,
                Maximum = 100,
                Foreground = Brushes.Blue
            };
            memoryPanel.Children.Add(_memoryProgressBar);
            
            _memoryText = new TextBlock { Text = "0 MB", HorizontalAlignment = HorizontalAlignment.Center };
            memoryPanel.Children.Add(_memoryText);
            
            Grid.SetColumn(memoryPanel, 1);
            grid.Children.Add(memoryPanel);
            
            return grid;
        }

        private async void UpdateTimer_Tick(object sender, EventArgs e)
        {
            await UpdateDashboardAsync();
        }

        private async Task UpdateDashboardAsync()
        {
            try
            {
                // Update performance stats
                var perfStats = _performanceMonitor.GetStats();
                UpdatePerformanceChart(perfStats);
                UpdateSystemStats(perfStats);
                
                // Update scheduling stats if available
                if (_schedulingService != null)
                {
                    var scheduleStats = _schedulingService.GetStatistics();
                    UpdateSchedulingStats(scheduleStats);
                    UpdateScheduledTasks();
                }
                
                // Update notification stats if available
                if (_notificationService != null)
                {
                    var notifStats = _notificationService.GetStatistics();
                    UpdateNotificationStats(notifStats);
                }
                
                // Update recent activities
                UpdateRecentActivities();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to update dashboard");
            }
        }

        private void UpdatePerformanceChart(PerformanceStats stats)
        {
            // Add current performance value
            var currentValue = stats.WorkingSetMB;
            _performanceHistory.Add(currentValue);
            
            // Keep only recent history
            while (_performanceHistory.Count > MaxHistoryPoints)
            {
                _performanceHistory.RemoveAt(0);
            }
            
            // Redraw chart
            _performanceChart.Children.Clear();
            
            if (_performanceHistory.Count < 2)
                return;
            
            var width = _performanceChart.ActualWidth;
            var height = _performanceChart.ActualHeight;
            
            if (width <= 0 || height <= 0)
                return;
            
            var maxValue = _performanceHistory.Max();
            var minValue = _performanceHistory.Min();
            var range = maxValue - minValue;
            
            if (range == 0) range = 1;
            
            var points = new PointCollection();
            for (int i = 0; i < _performanceHistory.Count; i++)
            {
                var x = (i / (double)(MaxHistoryPoints - 1)) * width;
                var y = height - ((_performanceHistory[i] - minValue) / range) * height;
                points.Add(new Point(x, y));
            }
            
            // Draw line
            var polyline = new Polyline
            {
                Points = points,
                Stroke = Brushes.Blue,
                StrokeThickness = 2
            };
            _performanceChart.Children.Add(polyline);
            
            // Draw grid lines
            for (int i = 0; i <= 4; i++)
            {
                var y = (height / 4) * i;
                var line = new Line
                {
                    X1 = 0,
                    Y1 = y,
                    X2 = width,
                    Y2 = y,
                    Stroke = Brushes.LightGray,
                    StrokeThickness = 0.5
                };
                _performanceChart.Children.Add(line);
            }
        }

        private void UpdateSystemStats(PerformanceStats stats)
        {
            // Update CPU usage (simulated)
            var cpuUsage = new Random().Next(10, 60); // Simulated CPU usage
            _cpuProgressBar.Value = cpuUsage;
            _cpuText.Text = $"{cpuUsage}%";
            
            // Update color based on usage
            _cpuProgressBar.Foreground = cpuUsage > 80 ? Brushes.Red : 
                                        cpuUsage > 50 ? Brushes.Orange : 
                                        Brushes.Green;
            
            // Update memory usage
            var memoryMB = stats.WorkingSetMB;
            var totalMemoryMB = 8192; // Simulated total memory
            var memoryPercent = (memoryMB / (double)totalMemoryMB) * 100;
            
            _memoryProgressBar.Value = memoryPercent;
            _memoryText.Text = $"{memoryMB} MB";
            
            // Update color based on usage
            _memoryProgressBar.Foreground = memoryPercent > 80 ? Brushes.Red : 
                                           memoryPercent > 50 ? Brushes.Orange : 
                                           Brushes.Blue;
        }

        private void UpdateSchedulingStats(SchedulingStatistics stats)
        {
            _totalRulesText.Text = stats.TotalTasks.ToString();
            _activeRulesText.Text = stats.EnabledTasks.ToString();
            _executionsText.Text = stats.TotalExecutions.ToString();
            
            var successRate = stats.TotalExecutions > 0 
                ? (stats.SuccessfulExecutions / (double)stats.TotalExecutions) * 100 
                : 0;
            _successRateText.Text = $"{successRate:F1}%";
        }

        private void UpdateScheduledTasks()
        {
            if (_schedulingService == null)
                return;
            
            _scheduledTasksPanel.Children.Clear();
            
            var tasks = _schedulingService.GetScheduledTasks()
                .Take(5)
                .OrderBy(t => t.NextRunTime);
            
            foreach (var task in tasks)
            {
                var taskPanel = new Border
                {
                    BorderBrush = Brushes.LightGray,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(3),
                    Margin = new Thickness(0, 2, 0, 2),
                    Padding = new Thickness(5)
                };
                
                var content = new StackPanel();
                
                content.Children.Add(new TextBlock
                {
                    Text = task.Id,
                    FontWeight = FontWeights.SemiBold
                });
                
                if (task.NextRunTime.HasValue)
                {
                    var timeUntil = task.NextRunTime.Value - DateTime.UtcNow;
                    content.Children.Add(new TextBlock
                    {
                        Text = $"Next: {FormatTimeSpan(timeUntil)}",
                        FontSize = 11,
                        Foreground = Brushes.Gray
                    });
                }
                
                content.Children.Add(new TextBlock
                {
                    Text = $"Executions: {task.ExecutionCount}",
                    FontSize = 11,
                    Foreground = Brushes.Gray
                });
                
                taskPanel.Child = content;
                _scheduledTasksPanel.Children.Add(taskPanel);
            }
        }

        private void UpdateNotificationStats(NotificationStatistics stats)
        {
            // Add to recent activities
            AddActivity($"Notifications: {stats.TotalSent} sent, {stats.QueuedCount} queued", 
                       ActivityType.Info);
        }

        private void UpdateRecentActivities()
        {
            _recentActivityList.Items.Clear();
            
            foreach (var activity in _recentActivities)
            {
                var item = new ListBoxItem
                {
                    Content = CreateActivityItem(activity)
                };
                _recentActivityList.Items.Add(item);
            }
        }

        private StackPanel CreateActivityItem(ActivityItem activity)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(2)
            };
            
            // Icon
            var icon = new Ellipse
            {
                Width = 8,
                Height = 8,
                Fill = GetActivityBrush(activity.Type),
                Margin = new Thickness(0, 0, 5, 0)
            };
            panel.Children.Add(icon);
            
            // Time
            var timeText = new TextBlock
            {
                Text = activity.Timestamp.ToString("HH:mm:ss"),
                FontSize = 10,
                Foreground = Brushes.Gray,
                Margin = new Thickness(0, 0, 5, 0)
            };
            panel.Children.Add(timeText);
            
            // Message
            var messageText = new TextBlock
            {
                Text = activity.Message,
                FontSize = 11
            };
            panel.Children.Add(messageText);
            
            return panel;
        }

        private void AddActivity(string message, ActivityType type)
        {
            var activity = new ActivityItem
            {
                Message = message,
                Type = type,
                Timestamp = DateTime.Now
            };
            
            _recentActivities.Enqueue(activity);
            
            while (_recentActivities.Count > MaxActivities)
            {
                _recentActivities.Dequeue();
            }
        }

        private Brush GetActivityBrush(ActivityType type)
        {
            return type switch
            {
                ActivityType.Success => Brushes.Green,
                ActivityType.Error => Brushes.Red,
                ActivityType.Warning => Brushes.Orange,
                _ => Brushes.Blue
            };
        }

        private string FormatTimeSpan(TimeSpan timeSpan)
        {
            if (timeSpan.TotalDays >= 1)
                return $"{(int)timeSpan.TotalDays}d {timeSpan.Hours}h";
            if (timeSpan.TotalHours >= 1)
                return $"{(int)timeSpan.TotalHours}h {timeSpan.Minutes}m";
            if (timeSpan.TotalMinutes >= 1)
                return $"{(int)timeSpan.TotalMinutes}m {timeSpan.Seconds}s";
            return $"{timeSpan.Seconds}s";
        }

        public void Dispose()
        {
            _updateTimer?.Stop();
        }

        // Supporting classes
        private class ActivityItem
        {
            public string Message { get; set; }
            public ActivityType Type { get; set; }
            public DateTime Timestamp { get; set; }
        }

        private enum ActivityType
        {
            Info,
            Success,
            Warning,
            Error
        }
    }
}
