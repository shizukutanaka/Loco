using System;

namespace Loco.Cli.UI.Models;

/// <summary>
/// Suggestion for a command based on user input
/// </summary>
public class CommandSuggestion
{
    /// <summary>
    /// Primary suggested command
    /// </summary>
    public string PrimaryCommand { get; set; } = "";

    /// <summary>
    /// Suggestion message in English
    /// </summary>
    public string Message { get; set; } = "";

    /// <summary>
    /// Suggestion message in Japanese
    /// </summary>
    public string MessageJA { get; set; } = "";

    /// <summary>
    /// Confidence score (0-1)
    /// </summary>
    public double Confidence { get; set; } = 0.0;

    /// <summary>
    /// Alternative suggestions
    /// </summary>
    public string[] Alternatives { get; set; } = Array.Empty<string>();
}
