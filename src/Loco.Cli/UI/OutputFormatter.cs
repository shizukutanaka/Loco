using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

namespace Loco.Cli.UI
{
    /// <summary>
    /// Output formatting utilities for different formats (JSON, XML, YAML, Table)
    /// </summary>
    public static class OutputFormatter
    {
        public enum Format
        {
            Text,
            Json,
            Xml,
            Yaml,
            Csv,
            Table
        }

        /// <summary>
        /// Format object based on specified format
        /// </summary>
        public static string FormatObject(object obj, Format format = Format.Text)
        {
            return format switch
            {
                Format.Json => FormatJson(obj),
                Format.Xml => FormatXml(obj),
                Format.Yaml => FormatYaml(obj),
                Format.Csv => FormatCsv(obj),
                Format.Table => FormatTable(obj),
                _ => obj?.ToString() ?? ""
            };
        }

        /// <summary>
        /// Format as JSON
        /// </summary>
        public static string FormatJson(object obj, bool indented = true)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = indented,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            return JsonSerializer.Serialize(obj, options);
        }

        /// <summary>
        /// Format as XML
        /// </summary>
        public static string FormatXml(object obj)
        {
            // Simple XML serialization
            var json = JsonSerializer.Serialize(obj);
            var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

            var xml = new XElement("root");
            foreach (var kvp in dict ?? new Dictionary<string, object>())
            {
                xml.Add(new XElement(kvp.Key, kvp.Value?.ToString() ?? ""));
            }

            return xml.ToString();
        }

        /// <summary>
        /// Format as YAML (simplified)
        /// </summary>
        public static string FormatYaml(object obj, int indent = 0)
        {
            var sb = new StringBuilder();
            var json = JsonSerializer.Serialize(obj);
            var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

            foreach (var kvp in dict ?? new Dictionary<string, object>())
            {
                sb.AppendLine($"{new string(' ', indent)}{kvp.Key}: {kvp.Value}");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Format as CSV
        /// </summary>
        public static string FormatCsv(object obj)
        {
            var sb = new StringBuilder();
            var json = JsonSerializer.Serialize(obj);

            if (obj is IEnumerable<object> list)
            {
                var items = list.ToArray();
                if (items.Length == 0)
                    return "";

                // Get headers from first item
                var firstJson = JsonSerializer.Serialize(items[0]);
                var firstDict = JsonSerializer.Deserialize<Dictionary<string, object>>(firstJson);
                var headers = firstDict?.Keys.ToArray() ?? Array.Empty<string>();

                // Write headers
                sb.AppendLine(string.Join(",", headers.Select(EscapeCsvValue)));

                // Write rows
                foreach (var item in items)
                {
                    var itemJson = JsonSerializer.Serialize(item);
                    var itemDict = JsonSerializer.Deserialize<Dictionary<string, object>>(itemJson);

                    var values = headers.Select(h =>
                        itemDict?.ContainsKey(h) == true
                            ? EscapeCsvValue(itemDict[h]?.ToString() ?? "")
                            : "");

                    sb.AppendLine(string.Join(",", values));
                }
            }
            else
            {
                var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                if (dict != null)
                {
                    sb.AppendLine(string.Join(",", dict.Keys.Select(EscapeCsvValue)));
                    sb.AppendLine(string.Join(",", dict.Values.Select(v => EscapeCsvValue(v?.ToString() ?? ""))));
                }
            }

            return sb.ToString();
        }

        private static string EscapeCsvValue(string value)
        {
            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
            {
                return $"\"{value.Replace("\"", "\"\"")}\"";
            }
            return value;
        }

        /// <summary>
        /// Format as table
        /// </summary>
        public static string FormatTable(object obj)
        {
            var sb = new StringBuilder();
            var json = JsonSerializer.Serialize(obj);

            if (obj is IEnumerable<object> list)
            {
                var items = list.ToArray();
                if (items.Length == 0)
                    return "";

                // Get headers from first item
                var firstJson = JsonSerializer.Serialize(items[0]);
                var firstDict = JsonSerializer.Deserialize<Dictionary<string, object>>(firstJson);
                var headers = firstDict?.Keys.ToArray() ?? Array.Empty<string>();

                // Calculate column widths
                var columnWidths = headers.ToDictionary(h => h, h => h.Length);

                foreach (var item in items)
                {
                    var itemJson = JsonSerializer.Serialize(item);
                    var itemDict = JsonSerializer.Deserialize<Dictionary<string, object>>(itemJson);

                    foreach (var header in headers)
                    {
                        if (itemDict?.ContainsKey(header) == true)
                        {
                            var value = itemDict[header]?.ToString() ?? "";
                            columnWidths[header] = Math.Max(columnWidths[header], value.Length);
                        }
                    }
                }

                // Build table
                var separator = "+" + string.Join("+", headers.Select(h => new string('-', columnWidths[h] + 2))) + "+";

                sb.AppendLine(separator);
                sb.Append("| ");
                sb.Append(string.Join(" | ", headers.Select(h => h.PadRight(columnWidths[h]))));
                sb.AppendLine(" |");
                sb.AppendLine(separator);

                foreach (var item in items)
                {
                    var itemJson = JsonSerializer.Serialize(item);
                    var itemDict = JsonSerializer.Deserialize<Dictionary<string, object>>(itemJson);

                    sb.Append("| ");
                    var values = headers.Select(h =>
                    {
                        var value = itemDict?.ContainsKey(h) == true
                            ? itemDict[h]?.ToString() ?? ""
                            : "";
                        return value.PadRight(columnWidths[h]);
                    });
                    sb.Append(string.Join(" | ", values));
                    sb.AppendLine(" |");
                }

                sb.AppendLine(separator);
            }
            else
            {
                var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                if (dict != null)
                {
                    var maxKeyLength = dict.Keys.Max(k => k.Length);

                    foreach (var kvp in dict)
                    {
                        sb.AppendLine($"{kvp.Key.PadRight(maxKeyLength)} : {kvp.Value}");
                    }
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Format file size in human-readable form
        /// </summary>
        public static string FormatBytes(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB", "PB" };
            int counter = 0;
            decimal number = bytes;

            while (Math.Round(number / 1024) >= 1)
            {
                number /= 1024;
                counter++;
                if (counter >= suffixes.Length - 1)
                    break;
            }

            return $"{number:n1} {suffixes[counter]}";
        }

        /// <summary>
        /// Format duration in human-readable form
        /// </summary>
        public static string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalSeconds < 1)
                return $"{duration.TotalMilliseconds:F0}ms";
            if (duration.TotalMinutes < 1)
                return $"{duration.TotalSeconds:F1}s";
            if (duration.TotalHours < 1)
                return $"{duration.TotalMinutes:F1}m";
            if (duration.TotalDays < 1)
                return $"{duration.TotalHours:F1}h";
            return $"{duration.TotalDays:F1}d";
        }

        /// <summary>
        /// Format number with thousands separator
        /// </summary>
        public static string FormatNumber(long number)
        {
            return number.ToString("N0");
        }

        /// <summary>
        /// Format percentage
        /// </summary>
        public static string FormatPercent(double value, int decimals = 1)
        {
            return $"{value.ToString($"F{decimals}")}%";
        }

        /// <summary>
        /// Truncate string with ellipsis
        /// </summary>
        public static string Truncate(string value, int maxLength, string ellipsis = "...")
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
                return value;

            return value.Substring(0, maxLength - ellipsis.Length) + ellipsis;
        }

        /// <summary>
        /// Format date/time in ISO format
        /// </summary>
        public static string FormatDateTime(DateTime dateTime, bool includeTime = true)
        {
            return includeTime
                ? dateTime.ToString("yyyy-MM-dd HH:mm:ss")
                : dateTime.ToString("yyyy-MM-dd");
        }

        /// <summary>
        /// Format relative time (e.g., "2 hours ago")
        /// </summary>
        public static string FormatRelativeTime(DateTime dateTime)
        {
            var timeSpan = DateTime.Now - dateTime;

            if (timeSpan.TotalSeconds < 60)
                return "just now";
            if (timeSpan.TotalMinutes < 60)
                return $"{(int)timeSpan.TotalMinutes} minute{(timeSpan.TotalMinutes >= 2 ? "s" : "")} ago";
            if (timeSpan.TotalHours < 24)
                return $"{(int)timeSpan.TotalHours} hour{(timeSpan.TotalHours >= 2 ? "s" : "")} ago";
            if (timeSpan.TotalDays < 30)
                return $"{(int)timeSpan.TotalDays} day{(timeSpan.TotalDays >= 2 ? "s" : "")} ago";
            if (timeSpan.TotalDays < 365)
                return $"{(int)(timeSpan.TotalDays / 30)} month{(timeSpan.TotalDays / 30 >= 2 ? "s" : "")} ago";
            return $"{(int)(timeSpan.TotalDays / 365)} year{(timeSpan.TotalDays / 365 >= 2 ? "s" : "")} ago";
        }

        /// <summary>
        /// Highlight text matches
        /// </summary>
        public static void HighlightMatch(string text, string searchTerm, ConsoleColor highlightColor = ConsoleColor.Yellow)
        {
            if (string.IsNullOrEmpty(searchTerm))
            {
                Console.WriteLine(text);
                return;
            }

            var index = text.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase);
            if (index == -1)
            {
                Console.WriteLine(text);
                return;
            }

            var before = text.Substring(0, index);
            var match = text.Substring(index, searchTerm.Length);
            var after = text.Substring(index + searchTerm.Length);

            Console.Write(before);
            Console.ForegroundColor = highlightColor;
            Console.Write(match);
            Console.ResetColor();
            Console.WriteLine(after);
        }
    }
}
