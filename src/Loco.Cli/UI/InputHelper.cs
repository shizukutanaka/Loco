using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Loco.Cli.UI
{
    /// <summary>
    /// Enhanced input handling with validation and auto-completion
    /// </summary>
    public static class InputHelper
    {
        /// <summary>
        /// Read input with auto-completion support
        /// </summary>
        public static string ReadLineWithCompletion(string[] completions)
        {
            var input = "";
            var cursorPosition = 0;

            while (true)
            {
                var key = Console.ReadKey(true);

                if (key.Key == ConsoleKey.Enter)
                {
                    Console.WriteLine();
                    return input;
                }
                else if (key.Key == ConsoleKey.Tab)
                {
                    // Auto-complete
                    var matches = completions
                        .Where(c => c.StartsWith(input, StringComparison.OrdinalIgnoreCase))
                        .ToArray();

                    if (matches.Length == 1)
                    {
                        // Clear current input
                        Console.Write("\r" + new string(' ', input.Length + 10) + "\r");
                        input = matches[0];
                        cursorPosition = input.Length;
                        Console.Write(input);
                    }
                    else if (matches.Length > 1)
                    {
                        // Show suggestions
                        Console.WriteLine();
                        Console.ForegroundColor = ConsoleUI.Colors.Info;
                        Console.WriteLine("Suggestions:");
                        foreach (var match in matches.Take(5))
                        {
                            Console.WriteLine($"  {match}");
                        }
                        if (matches.Length > 5)
                        {
                            Console.WriteLine($"  ... and {matches.Length - 5} more");
                        }
                        Console.ResetColor();
                        Console.Write(input);
                    }
                }
                else if (key.Key == ConsoleKey.Backspace && input.Length > 0)
                {
                    input = input.Substring(0, input.Length - 1);
                    cursorPosition--;
                    Console.Write("\b \b");
                }
                else if (!char.IsControl(key.KeyChar))
                {
                    input += key.KeyChar;
                    cursorPosition++;
                    Console.Write(key.KeyChar);
                }
            }
        }

        /// <summary>
        /// Validate email address
        /// </summary>
        public static bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            try
            {
                var regex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");
                return regex.IsMatch(email);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Validate path
        /// </summary>
        public static bool IsValidPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            try
            {
                var fullPath = System.IO.Path.GetFullPath(path);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Validate number range
        /// </summary>
        public static bool IsInRange(string input, int min, int max)
        {
            if (!int.TryParse(input, out var value))
                return false;

            return value >= min && value <= max;
        }

        /// <summary>
        /// Validate URL
        /// </summary>
        public static bool IsValidUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            return Uri.TryCreate(url, UriKind.Absolute, out var uriResult)
                && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
        }

        /// <summary>
        /// Prompt for integer within range
        /// </summary>
        public static int PromptInt(string message, int min, int max, int defaultValue)
        {
            while (true)
            {
                var input = ConsoleUI.Prompt(message, defaultValue.ToString());
                if (int.TryParse(input, out var value) && value >= min && value <= max)
                {
                    return value;
                }

                ConsoleUI.Error($"Please enter a number between {min} and {max}.");
            }
        }

        /// <summary>
        /// Prompt for path with validation
        /// </summary>
        public static string PromptPath(string message, string? defaultValue = null, bool mustExist = false)
        {
            while (true)
            {
                var input = ConsoleUI.Prompt(message, defaultValue);
                if (input == null)
                    continue;

                if (!IsValidPath(input))
                {
                    ConsoleUI.Error("Invalid path format.");
                    continue;
                }

                if (mustExist && !System.IO.Directory.Exists(input) && !System.IO.File.Exists(input))
                {
                    ConsoleUI.Error("Path does not exist.");
                    continue;
                }

                return input;
            }
        }

        /// <summary>
        /// Prompt for email
        /// </summary>
        public static string PromptEmail(string message, string? defaultValue = null)
        {
            while (true)
            {
                var input = ConsoleUI.Prompt(message, defaultValue);
                if (input == null)
                    continue;

                if (!IsValidEmail(input))
                {
                    ConsoleUI.Error("Invalid email format.");
                    continue;
                }

                return input;
            }
        }

        /// <summary>
        /// Prompt for choice from list
        /// </summary>
        public static string PromptChoice(string message, string[] choices, string? defaultValue = null)
        {
            while (true)
            {
                Console.WriteLine($"\n{message}");
                for (int i = 0; i < choices.Length; i++)
                {
                    var isDefault = choices[i] == defaultValue;
                    if (isDefault)
                    {
                        Console.ForegroundColor = ConsoleUI.Colors.Success;
                        Console.WriteLine($"  {i + 1}. {choices[i]} [DEFAULT]");
                        Console.ResetColor();
                    }
                    else
                    {
                        Console.WriteLine($"  {i + 1}. {choices[i]}");
                    }
                }

                Console.Write($"\nSelect (1-{choices.Length}): ");
                var input = Console.ReadLine()?.Trim();

                if (string.IsNullOrEmpty(input) && defaultValue != null)
                {
                    return defaultValue;
                }

                if (int.TryParse(input, out var choice) && choice >= 1 && choice <= choices.Length)
                {
                    return choices[choice - 1];
                }

                ConsoleUI.Error($"Invalid choice. Please enter 1-{choices.Length}.");
            }
        }

        /// <summary>
        /// Prompt for multi-line input
        /// </summary>
        public static string PromptMultiLine(string message, string endMarker = "EOF")
        {
            Console.WriteLine($"{message} (Type '{endMarker}' on a new line to finish):");
            var lines = new List<string>();

            while (true)
            {
                var line = Console.ReadLine();
                if (line?.Trim() == endMarker)
                    break;

                if (line != null)
                    lines.Add(line);
            }

            return string.Join(Environment.NewLine, lines);
        }

        /// <summary>
        /// Prompt for password (hidden input)
        /// </summary>
        public static string PromptPassword(string message = "Password")
        {
            Console.Write($"{message}: ");
            var password = "";

            while (true)
            {
                var key = Console.ReadKey(true);

                if (key.Key == ConsoleKey.Enter)
                {
                    Console.WriteLine();
                    return password;
                }
                else if (key.Key == ConsoleKey.Backspace && password.Length > 0)
                {
                    password = password.Substring(0, password.Length - 1);
                    Console.Write("\b \b");
                }
                else if (!char.IsControl(key.KeyChar))
                {
                    password += key.KeyChar;
                    Console.Write("*");
                }
            }
        }
    }
}
