using System;
using System.Collections.Generic;

namespace Loco.Cli.UI.Models;

/// <summary>
/// Contains help information for a CLI command
/// </summary>
public class CommandHelp
{
    /// <summary>
    /// Command name
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Alternative names for the command
    /// </summary>
    public string[] Aliases { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Category for grouping related commands
    /// </summary>
    public string Category { get; set; } = "General";

    /// <summary>
    /// Short one-line description
    /// </summary>
    public string ShortDescription { get; set; } = "";

    /// <summary>
    /// Detailed multi-line description
    /// </summary>
    public string LongDescription { get; set; } = "";

    /// <summary>
    /// How to use the command
    /// </summary>
    public string Usage { get; set; } = "";

    /// <summary>
    /// Command-line options and their descriptions
    /// </summary>
    public Dictionary<string, string>? Options { get; set; }

    /// <summary>
    /// Example usages
    /// </summary>
    public string[]? Examples { get; set; }

    /// <summary>
    /// Related commands
    /// </summary>
    public string[]? SeeAlso { get; set; }
}
