using System;
using System.IO;

namespace Loco.Core.Plugins
{
    /// <summary>
    /// Centralized helper for plugin-related filesystem paths.
    /// </summary>
    public static class PluginPaths
    {
        /// <summary>
        /// Environment variable name to override the plugins directory when no explicit path is provided.
        /// </summary>
        public const string PluginsPathEnvVarName = "LOCO_PLUGINS_PATH";

        /// <summary>
        /// Gets the default plugins directory path: %APPDATA%/Loco/Plugins on Windows.
        /// </summary>
        public static string GetDefaultPluginsDirectory()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Loco", "Plugins");
        }

        /// <summary>
        /// Resolves the effective plugins directory based on precedence:
        /// 1) Explicitly provided path (if not null/empty)
        /// 2) Environment variable LOCO_PLUGINS_PATH (if set and not empty)
        /// 3) Default path from GetDefaultPluginsDirectory()
        /// </summary>
        public static string GetEffectivePluginsDirectory(string provided = null)
        {
            if (!string.IsNullOrWhiteSpace(provided))
            {
                return provided;
            }

            var env = Environment.GetEnvironmentVariable(PluginsPathEnvVarName);
            if (!string.IsNullOrWhiteSpace(env))
            {
                return env;
            }

            return GetDefaultPluginsDirectory();
        }

        /// <summary>
        /// Ensures the specified directory exists, creating it if necessary.
        /// Returns the path for convenience chaining.
        /// </summary>
        public static string EnsureDirectory(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            return path;
        }

        /// <summary>
        /// Resolves the effective plugins directory and returns both the path and the source label.
        /// Source labels:
        ///  - "explicit" when an explicit path is provided
        ///  - $"env:{PluginsPathEnvVarName}" when taken from environment
        ///  - "default" when falling back to GetDefaultPluginsDirectory()
        /// </summary>
        public static (string path, string source) GetEffectivePluginsDirectoryWithSource(string provided = null)
        {
            if (!string.IsNullOrWhiteSpace(provided))
            {
                return (provided, "explicit");
            }

            var env = Environment.GetEnvironmentVariable(PluginsPathEnvVarName);
            if (!string.IsNullOrWhiteSpace(env))
            {
                return (env, $"env:{PluginsPathEnvVarName}");
            }

            return (GetDefaultPluginsDirectory(), "default");
        }
    }
}
