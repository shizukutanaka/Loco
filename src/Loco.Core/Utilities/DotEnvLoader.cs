using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Loco.Core.Utilities
{
    public static class DotEnvLoader
    {
        public static void Load(string? explicitPath = null)
        {
            try
            {
                string? path = explicitPath ?? FindDotEnv();
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;

                foreach (var (key, value) in Parse(File.ReadAllLines(path)))
                {
                    // Do not override existing environment variables
                    if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(key)))
                    {
                        Environment.SetEnvironmentVariable(key, value);
                    }
                }
            }
            catch
            {
                // best-effort; ignore errors
            }
        }

        private static string? FindDotEnv()
        {
            // Search current base directory upwards up to 3 levels
            var dir = AppContext.BaseDirectory;
            for (int i = 0; i < 4 && !string.IsNullOrEmpty(dir); i++)
            {
                var candidate = Path.Combine(dir, ".env");
                if (File.Exists(candidate)) return candidate;
                dir = Path.GetDirectoryName(dir);
            }
            return null;
        }

        private static IEnumerable<(string key, string value)> Parse(IEnumerable<string> lines)
        {
            foreach (var raw in lines)
            {
                var line = raw?.Trim();
                if (string.IsNullOrEmpty(line)) continue;
                if (line.StartsWith("#")) continue;
                var idx = line.IndexOf('=');
                if (idx <= 0) continue;
                var key = line.Substring(0, idx).Trim();
                var value = line.Substring(idx + 1).Trim();
                if (value.StartsWith("\"") && value.EndsWith("\""))
                    value = value.Substring(1, value.Length - 2);
                else if (value.StartsWith("'") && value.EndsWith("'"))
                    value = value.Substring(1, value.Length - 2);
                yield return (key, value);
            }
        }
    }
}
