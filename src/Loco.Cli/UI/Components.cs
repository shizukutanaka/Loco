using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Loco.Cli.UI
{
    /// <summary>
    /// Atlassian-inspired UI components for CLI
    /// Provides badge, tag, lozenge, banner, and more
    /// </summary>
    public static class Components
    {
        /// <summary>
        /// Badge component - small status indicator
        /// </summary>
        public static class Badge
        {
            public static void Show(string text, ConsoleColor color = ConsoleColor.Blue, bool inline = true)
            {
                Console.ForegroundColor = color;
                Console.Write($"[{text}]");
                Console.ResetColor();
                if (!inline) Console.WriteLine();
            }

            public static void Success(string text, bool inline = true) =>
                Show(text, DesignTokens.Colors.Semantic.Success, inline);

            public static void Warning(string text, bool inline = true) =>
                Show(text, DesignTokens.Colors.Semantic.Warning, inline);

            public static void Error(string text, bool inline = true) =>
                Show(text, DesignTokens.Colors.Semantic.Error, inline);

            public static void Info(string text, bool inline = true) =>
                Show(text, DesignTokens.Colors.Semantic.Info, inline);
        }

        /// <summary>
        /// Lozenge component - pill-shaped status indicator
        /// Inspired by Atlassian's lozenge component
        /// </summary>
        public static class Lozenge
        {
            public static void Show(string text, ConsoleColor bgColor = ConsoleColor.Blue, bool inline = true)
            {
                Console.ForegroundColor = ConsoleColor.Black;
                Console.BackgroundColor = bgColor;
                Console.Write($" {text} ");
                Console.ResetColor();
                if (!inline) Console.WriteLine();
            }

            public static void Success(string text, bool inline = true) =>
                Show(text, DesignTokens.Colors.Semantic.Success, inline);

            public static void Warning(string text, bool inline = true) =>
                Show(text, DesignTokens.Colors.Semantic.Warning, inline);

            public static void Error(string text, bool inline = true) =>
                Show(text, DesignTokens.Colors.Semantic.Error, inline);

            public static void Info(string text, bool inline = true) =>
                Show(text, DesignTokens.Colors.Semantic.Info, inline);

            public static void Default(string text, bool inline = true) =>
                Show(text, ConsoleColor.DarkGray, inline);
        }

        /// <summary>
        /// Banner component - full-width notification
        /// </summary>
        public static class Banner
        {
            public static void Show(string message, string? icon = null, ConsoleColor color = ConsoleColor.Blue)
            {
                var width = Math.Min(Console.WindowWidth - 4, 80);

                Console.WriteLine();
                Console.ForegroundColor = color;
                Console.WriteLine(new string(DesignTokens.Borders.Box.Horizontal, width));

                var displayMessage = icon != null ? $"{icon} {message}" : message;
                var padding = (width - displayMessage.Length) / 2;
                Console.Write(new string(' ', Math.Max(0, padding)));
                Console.WriteLine(displayMessage);

                Console.WriteLine(new string(DesignTokens.Borders.Box.Horizontal, width));
                Console.ResetColor();
                Console.WriteLine();
            }

            public static void Success(string message) =>
                Show(message, DesignTokens.Icons.Success, DesignTokens.Colors.Semantic.Success);

            public static void Warning(string message) =>
                Show(message, DesignTokens.Icons.Warning, DesignTokens.Colors.Semantic.Warning);

            public static void Error(string message) =>
                Show(message, DesignTokens.Icons.Error, DesignTokens.Colors.Semantic.Error);

            public static void Info(string message) =>
                Show(message, DesignTokens.Icons.Info, DesignTokens.Colors.Semantic.Info);
        }

        /// <summary>
        /// Card component - content container with border
        /// </summary>
        public static class Card
        {
            public static void Show(string title, string content, int width = 60, ConsoleColor borderColor = ConsoleColor.DarkGray)
            {
                Console.ForegroundColor = borderColor;

                // Top border
                Console.Write(DesignTokens.Borders.BoxRounded.TopLeft);
                Console.Write(new string(DesignTokens.Borders.BoxRounded.Horizontal, width - 2));
                Console.WriteLine(DesignTokens.Borders.BoxRounded.TopRight);

                // Title
                Console.Write(DesignTokens.Borders.BoxRounded.Vertical);
                Console.ForegroundColor = DesignTokens.Colors.Brand.Primary;
                Console.Write($" {title}");
                Console.ForegroundColor = borderColor;
                Console.Write(new string(' ', width - title.Length - 3));
                Console.WriteLine(DesignTokens.Borders.BoxRounded.Vertical);

                // Separator
                Console.Write(DesignTokens.Borders.Box.CrossLeft);
                Console.Write(new string(DesignTokens.Borders.BoxRounded.Horizontal, width - 2));
                Console.WriteLine(DesignTokens.Borders.Box.CrossRight);

                // Content
                var lines = content.Split('\n');
                foreach (var line in lines)
                {
                    Console.Write(DesignTokens.Borders.BoxRounded.Vertical);
                    Console.ResetColor();
                    Console.Write($" {line}");
                    Console.ForegroundColor = borderColor;
                    Console.Write(new string(' ', Math.Max(0, width - line.Length - 3)));
                    Console.WriteLine(DesignTokens.Borders.BoxRounded.Vertical);
                }

                // Bottom border
                Console.Write(DesignTokens.Borders.BoxRounded.BottomLeft);
                Console.Write(new string(DesignTokens.Borders.BoxRounded.Horizontal, width - 2));
                Console.WriteLine(DesignTokens.Borders.BoxRounded.BottomRight);
                Console.ResetColor();
            }
        }

        /// <summary>
        /// Section flag - visual separator with label
        /// </summary>
        public static class SectionFlag
        {
            public static void Show(string text, ConsoleColor color = ConsoleColor.Cyan)
            {
                Console.WriteLine();
                Console.ForegroundColor = color;
                Console.Write(DesignTokens.Icons.ArrowRight);
                Console.Write(" ");
                Console.ForegroundColor = DesignTokens.Colors.Neutral.Text;
                Console.WriteLine(text);
                Console.ForegroundColor = color;
                Console.WriteLine(new string(DesignTokens.Borders.Box.Horizontal, text.Length + 2));
                Console.ResetColor();
            }
        }

        /// <summary>
        /// Progress indicator - visual progress display
        /// </summary>
        public static class ProgressIndicator
        {
            public static void Show(int current, int total, string label = "", int width = 40)
            {
                var percent = (double)current / total;
                var filled = (int)(width * percent);
                var empty = width - filled;

                Console.Write("\r");

                if (!string.IsNullOrEmpty(label))
                {
                    Console.ForegroundColor = DesignTokens.Colors.Semantic.Info;
                    Console.Write($"{label}: ");
                    Console.ResetColor();
                }

                Console.ForegroundColor = DesignTokens.Colors.Neutral.Border;
                Console.Write("[");
                Console.ResetColor();

                Console.ForegroundColor = DesignTokens.Colors.Semantic.Success;
                Console.Write(new string('█', filled));
                Console.ResetColor();

                Console.ForegroundColor = DesignTokens.Colors.Neutral.BackgroundSubtle;
                Console.Write(new string('░', empty));
                Console.ResetColor();

                Console.ForegroundColor = DesignTokens.Colors.Neutral.Border;
                Console.Write("]");
                Console.ResetColor();

                Console.Write($" {percent:P0} ({current}/{total})");
            }

            public static void Complete(string label = "")
            {
                Console.Write("\r" + new string(' ', Console.WindowWidth - 1) + "\r");
                if (!string.IsNullOrEmpty(label))
                {
                    Console.ForegroundColor = DesignTokens.Colors.Semantic.Success;
                    Console.Write($"{DesignTokens.Icons.Success} ");
                    Console.ResetColor();
                    Console.WriteLine(label);
                }
            }
        }

        /// <summary>
        /// Breadcrumb - navigation trail
        /// </summary>
        public static class Breadcrumb
        {
            public static void Show(string[] items, string separator = " > ")
            {
                for (int i = 0; i < items.Length; i++)
                {
                    if (i == items.Length - 1)
                    {
                        Console.ForegroundColor = DesignTokens.Colors.Brand.Primary;
                        Console.Write(items[i]);
                        Console.ResetColor();
                    }
                    else
                    {
                        Console.ForegroundColor = DesignTokens.Colors.Neutral.TextSubtle;
                        Console.Write(items[i]);
                        Console.Write(separator);
                        Console.ResetColor();
                    }
                }
                Console.WriteLine();
            }
        }

        /// <summary>
        /// Divider - visual separator
        /// </summary>
        public static class Divider
        {
            public static void Show(int width = -1, char character = '─')
            {
                var actualWidth = width > 0 ? width : Math.Min(Console.WindowWidth, 80);
                Console.ForegroundColor = DesignTokens.Colors.Neutral.Border;
                Console.WriteLine(new string(character, actualWidth));
                Console.ResetColor();
            }

            public static void Thick(int width = -1)
            {
                Show(width, '═');
            }

            public static void Dotted(int width = -1)
            {
                var actualWidth = width > 0 ? width : Math.Min(Console.WindowWidth, 80);
                Console.ForegroundColor = DesignTokens.Colors.Neutral.Border;
                for (int i = 0; i < actualWidth; i++)
                {
                    Console.Write(i % 2 == 0 ? '·' : ' ');
                }
                Console.WriteLine();
                Console.ResetColor();
            }
        }

        /// <summary>
        /// Status indicator - animated status display
        /// </summary>
        public static class StatusIndicator
        {
            private static readonly string[] _spinnerFrames = { "⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏" };
            private static int _currentFrame = 0;

            public static void Spin(string message = "")
            {
                Console.Write($"\r{_spinnerFrames[_currentFrame]} {message}");
                _currentFrame = (_currentFrame + 1) % _spinnerFrames.Length;
            }

            public static void Complete(string message, bool success = true)
            {
                Console.Write("\r" + new string(' ', Console.WindowWidth - 1) + "\r");
                Console.ForegroundColor = success
                    ? DesignTokens.Colors.Semantic.Success
                    : DesignTokens.Colors.Semantic.Error;
                Console.Write(success ? DesignTokens.Icons.Success : DesignTokens.Icons.Error);
                Console.Write(" ");
                Console.ResetColor();
                Console.WriteLine(message);
            }
        }

        /// <summary>
        /// Inline dialog - small popup-like message
        /// </summary>
        public static class InlineDialog
        {
            public static void Show(string title, string message, ConsoleColor color = ConsoleColor.Blue)
            {
                var maxWidth = Math.Min(Console.WindowWidth - 10, 60);
                var lines = WrapText(message, maxWidth - 4);

                Console.WriteLine();
                Console.ForegroundColor = color;

                // Top
                Console.Write("  ");
                Console.Write(DesignTokens.Borders.BoxRounded.TopLeft);
                Console.Write(new string(DesignTokens.Borders.BoxRounded.Horizontal, maxWidth - 2));
                Console.WriteLine(DesignTokens.Borders.BoxRounded.TopRight);

                // Title
                Console.Write("  ");
                Console.Write(DesignTokens.Borders.BoxRounded.Vertical);
                Console.Write($" {title} ");
                Console.Write(new string(' ', maxWidth - title.Length - 3));
                Console.WriteLine(DesignTokens.Borders.BoxRounded.Vertical);

                // Content
                foreach (var line in lines)
                {
                    Console.Write("  ");
                    Console.Write(DesignTokens.Borders.BoxRounded.Vertical);
                    Console.ResetColor();
                    Console.Write($" {line}");
                    Console.ForegroundColor = color;
                    Console.Write(new string(' ', maxWidth - line.Length - 2));
                    Console.WriteLine(DesignTokens.Borders.BoxRounded.Vertical);
                }

                // Bottom
                Console.Write("  ");
                Console.Write(DesignTokens.Borders.BoxRounded.BottomLeft);
                Console.Write(new string(DesignTokens.Borders.BoxRounded.Horizontal, maxWidth - 2));
                Console.WriteLine(DesignTokens.Borders.BoxRounded.BottomRight);
                Console.ResetColor();
                Console.WriteLine();
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
    }
}
