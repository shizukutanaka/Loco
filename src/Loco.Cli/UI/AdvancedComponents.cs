using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Loco.Cli.UI
{
    /// <summary>
    /// Advanced UI components inspired by Atlassian Design System
    /// Modals, Forms, Data Grids, and more
    /// </summary>
    public static class AdvancedComponents
    {
        /// <summary>
        /// Modal dialog component
        /// </summary>
        public static class Modal
        {
            public static bool Show(string title, string content, string[] actions = null, int width = 60)
            {
                actions ??= new[] { "OK", "Cancel" };
                var lines = WrapText(content, width - 6);

                Console.WriteLine();

                // Backdrop
                Console.ForegroundColor = DesignTokens.Colors.Neutral.BackgroundSubtle;
                for (int i = 0; i < 3; i++)
                {
                    Console.WriteLine(new string('░', Math.Min(Console.WindowWidth, width + 10)));
                }
                Console.ResetColor();

                // Modal box
                var indent = "    ";
                Console.ForegroundColor = DesignTokens.Colors.Brand.Primary;

                // Top
                Console.Write(indent);
                Console.Write(DesignTokens.Borders.BoxBold.TopLeft);
                Console.Write(new string(DesignTokens.Borders.BoxBold.Horizontal, width - 2));
                Console.WriteLine(DesignTokens.Borders.BoxBold.TopRight);

                // Title
                Console.Write(indent);
                Console.Write(DesignTokens.Borders.BoxBold.Vertical);
                Console.ForegroundColor = DesignTokens.Colors.Neutral.Text;
                Console.Write($" {title} ");
                Console.ForegroundColor = DesignTokens.Colors.Brand.Primary;
                Console.Write(new string(' ', width - title.Length - 3));
                Console.WriteLine(DesignTokens.Borders.BoxBold.Vertical);

                // Separator
                Console.Write(indent);
                Console.Write(DesignTokens.Borders.Box.CrossLeft);
                Console.Write(new string(DesignTokens.Borders.Box.Horizontal, width - 2));
                Console.WriteLine(DesignTokens.Borders.Box.CrossRight);

                // Content
                Console.ResetColor();
                foreach (var line in lines)
                {
                    Console.Write(indent);
                    Console.ForegroundColor = DesignTokens.Colors.Brand.Primary;
                    Console.Write(DesignTokens.Borders.BoxBold.Vertical);
                    Console.ResetColor();
                    Console.Write($" {line}");
                    Console.ForegroundColor = DesignTokens.Colors.Brand.Primary;
                    Console.Write(new string(' ', width - line.Length - 2));
                    Console.WriteLine(DesignTokens.Borders.BoxBold.Vertical);
                }

                // Actions separator
                Console.Write(indent);
                Console.Write(DesignTokens.Borders.Box.CrossLeft);
                Console.Write(new string(DesignTokens.Borders.Box.Horizontal, width - 2));
                Console.WriteLine(DesignTokens.Borders.Box.CrossRight);

                // Actions
                Console.Write(indent);
                Console.Write(DesignTokens.Borders.BoxBold.Vertical);
                Console.ResetColor();

                var actionStr = string.Join("  ", actions.Select((a, i) =>
                    i == 0 ? $"[{i + 1}] {a}" : $"[{i + 1}] {a}"));
                Console.Write($" {actionStr}");

                Console.ForegroundColor = DesignTokens.Colors.Brand.Primary;
                Console.Write(new string(' ', width - actionStr.Length - 2));
                Console.WriteLine(DesignTokens.Borders.BoxBold.Vertical);

                // Bottom
                Console.Write(indent);
                Console.Write(DesignTokens.Borders.BoxBold.BottomLeft);
                Console.Write(new string(DesignTokens.Borders.BoxBold.Horizontal, width - 2));
                Console.WriteLine(DesignTokens.Borders.BoxBold.BottomRight);
                Console.ResetColor();

                Console.WriteLine();

                // Get user input
                Console.Write("Choose action (1-" + actions.Length + "): ");
                var input = Console.ReadLine();
                return input == "1";
            }

            private static List<string> WrapText(string text, int maxWidth)
            {
                var lines = new List<string>();
                var words = text.Split(' ');
                var currentLine = "";

                foreach (var word in words)
                {
                    if ((currentLine + word).Length > maxWidth)
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
        /// Form builder component
        /// </summary>
        public static class Form
        {
            public class Field
            {
                public string Label { get; set; } = "";
                public string Placeholder { get; set; } = "";
                public string DefaultValue { get; set; } = "";
                public bool Required { get; set; } = false;
                public Func<string, (bool valid, string error)>? Validator { get; set; }
            }

            public static Dictionary<string, string> Show(string title, Dictionary<string, Field> fields)
            {
                Console.WriteLine();
                Components.SectionFlag.Show(title, DesignTokens.Colors.Brand.Primary);
                Console.WriteLine();

                var results = new Dictionary<string, string>();

                foreach (var kvp in fields)
                {
                    var fieldName = kvp.Key;
                    var field = kvp.Value;

                    while (true)
                    {
                        // Label
                        Console.ForegroundColor = DesignTokens.Colors.Neutral.Text;
                        Console.Write(field.Label);

                        if (field.Required)
                        {
                            Console.ForegroundColor = DesignTokens.Colors.Semantic.Error;
                            Console.Write(" *");
                        }

                        Console.ResetColor();
                        Console.WriteLine();

                        // Placeholder hint
                        if (!string.IsNullOrEmpty(field.Placeholder))
                        {
                            Console.ForegroundColor = DesignTokens.Colors.Neutral.TextSubtle;
                            Console.WriteLine($"  {field.Placeholder}");
                            Console.ResetColor();
                        }

                        // Input
                        Console.Write("  > ");
                        var input = Console.ReadLine()?.Trim() ?? "";

                        // Use default if empty
                        if (string.IsNullOrEmpty(input) && !string.IsNullOrEmpty(field.DefaultValue))
                        {
                            input = field.DefaultValue;
                        }

                        // Validation
                        if (field.Required && string.IsNullOrEmpty(input))
                        {
                            Console.ForegroundColor = DesignTokens.Colors.Semantic.Error;
                            Console.WriteLine($"  {DesignTokens.Icons.Error} This field is required");
                            Console.ResetColor();
                            continue;
                        }

                        if (field.Validator != null && !string.IsNullOrEmpty(input))
                        {
                            var (valid, error) = field.Validator(input);
                            if (!valid)
                            {
                                Console.ForegroundColor = DesignTokens.Colors.Semantic.Error;
                                Console.WriteLine($"  {DesignTokens.Icons.Error} {error}");
                                Console.ResetColor();
                                continue;
                            }
                        }

                        results[fieldName] = input;
                        Console.ForegroundColor = DesignTokens.Colors.Semantic.Success;
                        Console.WriteLine($"  {DesignTokens.Icons.Success} OK");
                        Console.ResetColor();
                        Console.WriteLine();
                        break;
                    }
                }

                return results;
            }
        }

        /// <summary>
        /// Notification toast component
        /// </summary>
        public static class Toast
        {
            public static void Show(string message, string type = "info", int durationMs = 3000)
            {
                var icon = type switch
                {
                    "success" => DesignTokens.Icons.Success,
                    "error" => DesignTokens.Icons.Error,
                    "warning" => DesignTokens.Icons.Warning,
                    _ => DesignTokens.Icons.Info
                };

                var color = type switch
                {
                    "success" => DesignTokens.Colors.Semantic.Success,
                    "error" => DesignTokens.Colors.Semantic.Error,
                    "warning" => DesignTokens.Colors.Semantic.Warning,
                    _ => DesignTokens.Colors.Semantic.Info
                };

                // Save cursor position
                var top = Console.CursorTop;
                var left = Console.CursorLeft;

                // Show toast at bottom right
                var toastWidth = Math.Min(50, message.Length + 6);
                var toastLeft = Console.WindowWidth - toastWidth - 2;
                var toastTop = Console.WindowHeight - 4;

                Console.SetCursorPosition(toastLeft, toastTop);
                Console.ForegroundColor = color;
                Console.Write(DesignTokens.Borders.BoxRounded.TopLeft);
                Console.Write(new string(DesignTokens.Borders.BoxRounded.Horizontal, toastWidth - 2));
                Console.WriteLine(DesignTokens.Borders.BoxRounded.TopRight);

                Console.SetCursorPosition(toastLeft, toastTop + 1);
                Console.Write(DesignTokens.Borders.BoxRounded.Vertical);
                Console.ResetColor();
                Console.Write($" {icon} {message.PadRight(toastWidth - 5)}");
                Console.ForegroundColor = color;
                Console.WriteLine(DesignTokens.Borders.BoxRounded.Vertical);

                Console.SetCursorPosition(toastLeft, toastTop + 2);
                Console.Write(DesignTokens.Borders.BoxRounded.BottomLeft);
                Console.Write(new string(DesignTokens.Borders.BoxRounded.Horizontal, toastWidth - 2));
                Console.WriteLine(DesignTokens.Borders.BoxRounded.BottomRight);
                Console.ResetColor();

                // Restore cursor
                Console.SetCursorPosition(left, top);

                // Auto-dismiss
                Task.Run(async () =>
                {
                    await Task.Delay(durationMs);
                    // Clear toast area
                    for (int i = 0; i < 3; i++)
                    {
                        Console.SetCursorPosition(toastLeft, toastTop + i);
                        Console.Write(new string(' ', toastWidth));
                    }
                });
            }
        }

        /// <summary>
        /// Enhanced data grid with sorting and filtering
        /// </summary>
        public static class DataGrid
        {
            public class Column
            {
                public string Header { get; set; } = "";
                public Func<object, string> Formatter { get; set; } = obj => obj?.ToString() ?? "";
                public bool Sortable { get; set; } = true;
                public int Width { get; set; } = 15;
            }

            public static void Show<T>(List<T> data, Dictionary<string, Column> columns, bool interactive = false)
            {
                if (data.Count == 0)
                {
                    ConsoleUI.Info("No data to display", "表示するデータがありません");
                    return;
                }

                var headers = columns.Keys.ToArray();
                var rows = data.Select(item =>
                {
                    return columns.Select(kvp =>
                    {
                        var value = typeof(T).GetProperty(kvp.Key)?.GetValue(item);
                        return kvp.Value.Formatter(value ?? "");
                    }).ToArray();
                }).ToList();

                // Header
                Console.ForegroundColor = DesignTokens.Colors.Brand.Primary;
                Console.Write(DesignTokens.Borders.Box.TopLeft);
                foreach (var header in headers)
                {
                    var width = columns[header].Width;
                    Console.Write(new string(DesignTokens.Borders.Box.Horizontal, width));
                    Console.Write(DesignTokens.Borders.Box.CrossTop);
                }
                Console.SetCursorPosition(Console.CursorLeft - 1, Console.CursorTop);
                Console.WriteLine(DesignTokens.Borders.Box.TopRight);

                // Column headers
                Console.Write(DesignTokens.Borders.Box.Vertical);
                foreach (var header in headers)
                {
                    var width = columns[header].Width;
                    Console.ForegroundColor = DesignTokens.Colors.Neutral.Text;
                    var sortIndicator = columns[header].Sortable ? " ↕" : "";
                    Console.Write($" {header.PadRight(width - sortIndicator.Length - 2)}{sortIndicator} ");
                    Console.ForegroundColor = DesignTokens.Colors.Brand.Primary;
                    Console.Write(DesignTokens.Borders.Box.Vertical);
                }
                Console.WriteLine();

                // Separator
                Console.Write(DesignTokens.Borders.Box.CrossLeft);
                foreach (var header in headers)
                {
                    var width = columns[header].Width;
                    Console.Write(new string(DesignTokens.Borders.Box.Horizontal, width));
                    Console.Write(DesignTokens.Borders.Box.Cross);
                }
                Console.SetCursorPosition(Console.CursorLeft - 1, Console.CursorTop);
                Console.WriteLine(DesignTokens.Borders.Box.CrossRight);

                // Rows
                Console.ResetColor();
                foreach (var row in rows)
                {
                    Console.ForegroundColor = DesignTokens.Colors.Brand.Primary;
                    Console.Write(DesignTokens.Borders.Box.Vertical);
                    Console.ResetColor();

                    for (int i = 0; i < row.Length; i++)
                    {
                        var width = columns[headers[i]].Width;
                        var value = row[i].Length > width - 2
                            ? row[i].Substring(0, width - 5) + "..."
                            : row[i];
                        Console.Write($" {value.PadRight(width - 2)} ");
                        Console.ForegroundColor = DesignTokens.Colors.Brand.Primary;
                        Console.Write(DesignTokens.Borders.Box.Vertical);
                        Console.ResetColor();
                    }
                    Console.WriteLine();
                }

                // Bottom
                Console.ForegroundColor = DesignTokens.Colors.Brand.Primary;
                Console.Write(DesignTokens.Borders.Box.BottomLeft);
                foreach (var header in headers)
                {
                    var width = columns[header].Width;
                    Console.Write(new string(DesignTokens.Borders.Box.Horizontal, width));
                    Console.Write(DesignTokens.Borders.Box.CrossBottom);
                }
                Console.SetCursorPosition(Console.CursorLeft - 1, Console.CursorTop);
                Console.WriteLine(DesignTokens.Borders.Box.BottomRight);
                Console.ResetColor();

                // Footer info
                Console.ForegroundColor = DesignTokens.Colors.Neutral.TextSubtle;
                Console.WriteLine($"\nShowing {rows.Count} of {rows.Count} items");
                Console.ResetColor();
            }
        }

        /// <summary>
        /// Tabs component
        /// </summary>
        public static class Tabs
        {
            public static int Show(string[] tabNames, string[] tabContents = null)
            {
                Console.WriteLine();

                // Draw tabs
                for (int i = 0; i < tabNames.Length; i++)
                {
                    var isFirst = i == 0;

                    Console.ForegroundColor = isFirst
                        ? DesignTokens.Colors.Brand.Primary
                        : DesignTokens.Colors.Neutral.TextSubtle;

                    Console.Write(isFirst ? " [" : "  ");
                    Console.Write(tabNames[i]);
                    Console.Write(isFirst ? "] " : "  ");
                    Console.ResetColor();
                }
                Console.WriteLine();

                // Underline for active tab
                Console.ForegroundColor = DesignTokens.Colors.Brand.Primary;
                Console.WriteLine(new string(DesignTokens.Borders.Box.Horizontal, tabNames[0].Length + 3));
                Console.ResetColor();

                // Show content if provided
                if (tabContents != null && tabContents.Length > 0)
                {
                    Console.WriteLine();
                    Console.WriteLine(tabContents[0]);
                }

                return 0;
            }
        }

        /// <summary>
        /// Accordion component
        /// </summary>
        public static class Accordion
        {
            public static void Show(Dictionary<string, string> sections, bool expandFirst = true)
            {
                var expandedSections = new HashSet<string>();
                if (expandFirst && sections.Count > 0)
                {
                    expandedSections.Add(sections.Keys.First());
                }

                Console.WriteLine();

                foreach (var section in sections)
                {
                    var isExpanded = expandedSections.Contains(section.Key);
                    var icon = isExpanded ? "▼" : "▶";

                    Console.ForegroundColor = DesignTokens.Colors.Brand.Primary;
                    Console.Write($"{icon} ");
                    Console.ForegroundColor = DesignTokens.Colors.Neutral.Text;
                    Console.WriteLine(section.Key);
                    Console.ResetColor();

                    if (isExpanded)
                    {
                        Console.ForegroundColor = DesignTokens.Colors.Neutral.TextSubtle;
                        var lines = section.Value.Split('\n');
                        foreach (var line in lines)
                        {
                            Console.WriteLine($"  {line}");
                        }
                        Console.ResetColor();
                    }

                    Console.WriteLine();
                }
            }
        }
    }
}
