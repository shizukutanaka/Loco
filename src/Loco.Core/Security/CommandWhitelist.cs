using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Loco.Core.Security
{
    /// <summary>
    /// Configurable command whitelist for process execution security
    /// </summary>
    public static class CommandWhitelist
    {
        private static readonly HashSet<string> DefaultCommands = new(StringComparer.OrdinalIgnoreCase)
        {
            "cmd.exe", "powershell.exe", "dotnet.exe", "git.exe",
            "node.exe", "npm.exe", "python.exe", "robocopy.exe",
            "xcopy.exe", "tar.exe", "7z.exe", "curl.exe", "wget.exe"
        };

        private static HashSet<string>? _customCommands;
        private static readonly object _lock = new();

        /// <summary>
        /// Get the effective whitelist (custom if loaded, otherwise default)
        /// </summary>
        public static IReadOnlySet<string> GetAllowedCommands()
        {
            lock (_lock)
            {
                return _customCommands ?? DefaultCommands;
            }
        }

        /// <summary>
        /// Load custom whitelist from configuration file
        /// </summary>
        public static bool LoadFromFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    return false;

                var json = File.ReadAllText(filePath);
                var commands = JsonSerializer.Deserialize<string[]>(json);

                if (commands != null && commands.Length > 0)
                {
                    lock (_lock)
                    {
                        _customCommands = new HashSet<string>(commands, StringComparer.OrdinalIgnoreCase);
                    }
                    return true;
                }
            }
            catch
            {
                // Fall back to defaults on error
            }

            return false;
        }

        /// <summary>
        /// Check if a command is allowed
        /// </summary>
        public static bool IsAllowed(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
                return false;

            var commandName = Path.GetFileName(command).ToLowerInvariant();
            return GetAllowedCommands().Contains(commandName);
        }

        /// <summary>
        /// Reset to default whitelist
        /// </summary>
        public static void ResetToDefaults()
        {
            lock (_lock)
            {
                _customCommands = null;
            }
        }
    }
}
