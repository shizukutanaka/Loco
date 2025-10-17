using System.Text.Json;
using System.Text.Json.Serialization;

namespace Loco.Core.Utilities
{
    /// <summary>
    /// Provides standard JSON serialization options for consistent formatting across the application.
    /// </summary>
    public static class JsonDefaults
    {
        /// <summary>
        /// Standard indented JSON options for human-readable output.
        /// Uses camelCase naming and writes indented (pretty-printed) JSON.
        /// </summary>
        public static readonly JsonSerializerOptions Indented = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true
        };

        /// <summary>
        /// Compact JSON options for minimal output size.
        /// Uses camelCase naming without indentation.
        /// </summary>
        public static readonly JsonSerializerOptions Compact = new()
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true
        };

        /// <summary>
        /// JSON options for configuration files.
        /// Includes comments and trailing commas for better editability.
        /// </summary>
        public static readonly JsonSerializerOptions Configuration = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };
    }
}
