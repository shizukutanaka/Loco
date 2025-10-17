using System;
using System.Collections.Generic;
using System.Linq;

namespace Loco.Cli.UI
{
    /// <summary>
    /// Responsive layout system for CLI
    /// Adapts to terminal width and provides consistent spacing
    /// </summary>
    public static class Layout
    {
        /// <summary>
        /// Get current terminal size category
        /// </summary>
        public enum TerminalSize
        {
            XSmall,  // < 40 cols
            Small,   // 40-60 cols
            Medium,  // 60-80 cols
            Large,   // 80-120 cols
            XLarge   // > 120 cols
        }

        /// <summary>
        /// Get current terminal size
        /// </summary>
        public static TerminalSize GetTerminalSize()
        {
            var width = Console.WindowWidth;

            return width switch
            {
                < 40 => TerminalSize.XSmall,
                < 60 => TerminalSize.Small,
                < 80 => TerminalSize.Medium,
                < 120 => TerminalSize.Large,
                _ => TerminalSize.XLarge
            };
        }

        /// <summary>
        /// Get optimal content width for current terminal
        /// </summary>
        public static int GetContentWidth(int maxWidth = 120)
        {
            var terminalWidth = Console.WindowWidth;
            var padding = GetTerminalSize() switch
            {
                TerminalSize.XSmall => 2,
                TerminalSize.Small => 4,
                TerminalSize.Medium => 6,
                TerminalSize.Large => 8,
                TerminalSize.XLarge => 10,
                _ => 4
            };

            return Math.Min(terminalWidth - (padding * 2), maxWidth);
        }

        /// <summary>
        /// Column layout system
        /// </summary>
        public static class Columns
        {
            public static void Show(params (string content, int weight)[] columns)
            {
                var totalWeight = columns.Sum(c => c.weight);
                var contentWidth = GetContentWidth();

                var columnWidths = columns.Select(c =>
                    (int)(contentWidth * ((double)c.weight / totalWeight))
                ).ToArray();

                // Adjust for rounding errors
                var diff = contentWidth - columnWidths.Sum();
                if (diff != 0)
                {
                    columnWidths[0] += diff;
                }

                for (int i = 0; i < columns.Length; i++)
                {
                    var lines = WrapText(columns[i].content, columnWidths[i]);
                    Console.Write(lines.FirstOrDefault()?.PadRight(columnWidths[i]) ?? new string(' ', columnWidths[i]));
                    Console.Write(DesignTokens.Spacing.Get(DesignTokens.Spacing.Small));
                }
                Console.WriteLine();
            }

            private static List<string> WrapText(string text, int width)
            {
                var lines = new List<string>();
                var words = text.Split(' ');
                var currentLine = "";

                foreach (var word in words)
                {
                    if ((currentLine + word).Length > width)
                    {
                        if (!string.IsNullOrEmpty(currentLine))
                        {
                            lines.Add(currentLine.Trim());
                        }
                        currentLine = word + " ";
                    }
                    else
                    {
                        currentLine += word + " ";
                    }
                }

                if (!string.IsNullOrEmpty(currentLine))
                {
                    lines.Add(currentLine.Trim());
                }

                return lines;
            }
        }

        /// <summary>
        /// Grid system
        /// </summary>
        public static class Grid
        {
            public static void Show(string[][] items, int columns = 3)
            {
                var contentWidth = GetContentWidth();
                var columnWidth = contentWidth / columns;
                var spacing = DesignTokens.Spacing.Small;

                for (int row = 0; row < items.Length; row++)
                {
                    for (int col = 0; col < Math.Min(items[row].Length, columns); col++)
                    {
                        var item = items[row][col];
                        var truncated = item.Length > columnWidth - spacing
                            ? item.Substring(0, columnWidth - spacing - 3) + "..."
                            : item;

                        Console.Write(truncated.PadRight(columnWidth));
                    }
                    Console.WriteLine();
                }
            }
        }

        /// <summary>
        /// Container with padding
        /// </summary>
        public static class Container
        {
            public static void Show(Action content, int padding = -1)
            {
                var actualPadding = padding > 0 ? padding : GetTerminalSize() switch
                {
                    TerminalSize.XSmall => DesignTokens.Spacing.Small,
                    TerminalSize.Small => DesignTokens.Spacing.Medium,
                    _ => DesignTokens.Spacing.Large
                };

                Console.Write(DesignTokens.Spacing.Get(actualPadding));
                content();
            }
        }

        /// <summary>
        /// Responsive table
        /// </summary>
        public static class ResponsiveTable
        {
            public static void Show(string[] headers, List<string[]> rows, bool adaptToTerminal = true)
            {
                var terminalSize = GetTerminalSize();

                if (adaptToTerminal && terminalSize <= TerminalSize.Small)
                {
                    // Card layout for small terminals
                    ShowAsCards(headers, rows);
                }
                else
                {
                    // Standard table layout
                    ConsoleUI.ShowTable(headers, rows);
                }
            }

            private static void ShowAsCards(string[] headers, List<string[]> rows)
            {
                foreach (var row in rows)
                {
                    Components.Card.Show(
                        headers[0] + ": " + row[0],
                        string.Join("\n", headers.Skip(1).Zip(row.Skip(1), (h, v) => $"{h}: {v}"))
                    );
                    Console.WriteLine();
                }
            }
        }

        /// <summary>
        /// Stack layout - vertical arrangement with spacing
        /// </summary>
        public static class Stack
        {
            public static void Show(Action[] items, int spacing = -1)
            {
                var actualSpacing = spacing > 0 ? spacing : DesignTokens.Spacing.Medium;

                for (int i = 0; i < items.Length; i++)
                {
                    items[i]();

                    if (i < items.Length - 1)
                    {
                        for (int j = 0; j < actualSpacing / 4; j++) // Approximate line spacing
                        {
                            Console.WriteLine();
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Inline layout - horizontal arrangement
        /// </summary>
        public static class Inline
        {
            public static void Show(string[] items, int spacing = -1)
            {
                var actualSpacing = spacing > 0 ? spacing : DesignTokens.Spacing.Medium;

                for (int i = 0; i < items.Length; i++)
                {
                    Console.Write(items[i]);

                    if (i < items.Length - 1)
                    {
                        Console.Write(DesignTokens.Spacing.Get(actualSpacing));
                    }
                }
                Console.WriteLine();
            }
        }

        /// <summary>
        /// Sidebar layout - two-column layout
        /// </summary>
        public static class Sidebar
        {
            public static void Show(string sidebar, string main, int sidebarWidth = 20)
            {
                var contentWidth = GetContentWidth();
                var mainWidth = contentWidth - sidebarWidth - DesignTokens.Spacing.Large;

                var sidebarLines = sidebar.Split('\n');
                var mainLines = main.Split('\n');

                var maxLines = Math.Max(sidebarLines.Length, mainLines.Length);

                for (int i = 0; i < maxLines; i++)
                {
                    // Sidebar
                    var sidebarLine = i < sidebarLines.Length ? sidebarLines[i] : "";
                    Console.ForegroundColor = DesignTokens.Colors.Neutral.TextSubtle;
                    Console.Write(sidebarLine.PadRight(sidebarWidth));
                    Console.ResetColor();

                    // Spacing
                    Console.Write(DesignTokens.Spacing.Get(DesignTokens.Spacing.Large));

                    // Main content
                    var mainLine = i < mainLines.Length ? mainLines[i] : "";
                    Console.WriteLine(mainLine);
                }
            }
        }
    }

    /// <summary>
    /// Accessibility helpers
    /// </summary>
    public static class Accessibility
    {
        /// <summary>
        /// Announce to screen reader (simulated for CLI)
        /// </summary>
        public static void Announce(string message, bool assertive = false)
        {
            // In a real implementation, this would use platform-specific APIs
            // For CLI, we output to stderr with a prefix
            var prefix = assertive ? "[IMPORTANT]" : "[INFO]";
            Console.Error.WriteLine($"{prefix} {message}");
        }

        /// <summary>
        /// Show keyboard shortcuts
        /// </summary>
        public static void ShowKeyboardShortcuts(Dictionary<string, string> shortcuts)
        {
            Components.SectionFlag.Show("Keyboard Shortcuts", DesignTokens.Colors.Semantic.Info);

            foreach (var shortcut in shortcuts)
            {
                Console.ForegroundColor = DesignTokens.Colors.Brand.Primary;
                Console.Write($"  {shortcut.Key.PadRight(15)}");
                Console.ResetColor();
                Console.WriteLine($" {shortcut.Value}");
            }

            Console.WriteLine();
        }

        /// <summary>
        /// Create skip link (for long output)
        /// </summary>
        public static void ShowSkipLink(string target)
        {
            Console.ForegroundColor = DesignTokens.Colors.Interactive.Focus;
            Console.WriteLine($"[Press any key to skip to {target}]");
            Console.ResetColor();
        }

        /// <summary>
        /// High contrast mode detection
        /// </summary>
        public static bool IsHighContrastMode()
        {
            // Check if terminal supports high contrast
            // This is a placeholder - actual implementation would check terminal capabilities
            return false;
        }

        /// <summary>
        /// Screen reader mode
        /// </summary>
        private static bool _screenReaderMode = false;

        public static void EnableScreenReaderMode(bool enable = true)
        {
            _screenReaderMode = enable;
        }

        public static bool IsScreenReaderMode() => _screenReaderMode;

        /// <summary>
        /// Describe UI element for screen readers
        /// </summary>
        public static void Describe(string element, string description)
        {
            if (_screenReaderMode)
            {
                Console.Error.WriteLine($"[DESCRIPTION] {element}: {description}");
            }
        }
    }
}
