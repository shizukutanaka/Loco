using System;
using System.IO;
using System.Text.Json;

namespace Loco.UI.Models;

/// <summary>
/// User settings model for the application
/// </summary>
public class UserSettings
{
    /// <summary>
    /// Selected LLM model
    /// </summary>
    public string SelectedModel { get; set; } = "local-model-1 (推奨)";
    
    /// <summary>
    /// Selected LLM model ID (stable identifier). Optional for backward compatibility.
    /// </summary>
    public string SelectedModelId { get; set; } = string.Empty;
    
    /// <summary>
    /// Whether to automatically start flows on startup
    /// </summary>
    public bool AutoStartFlows { get; set; } = false;
    
    /// <summary>
    /// Whether to show notifications on errors
    /// </summary>
    public bool ShowErrorNotifications { get; set; } = true;
    
    /// <summary>
    /// Whether to record detailed logs
    /// </summary>
    public bool RecordDetailedLogs { get; set; } = false;
    
    /// <summary>
    /// Whether to enable sandbox execution
    /// </summary>
    public bool EnableSandboxExecution { get; set; } = true;
    
    /// <summary>
    /// Whether to restrict network access
    /// </summary>
    public bool RestrictNetworkAccess { get; set; } = false;
    
    /// <summary>
    /// Whether to restrict file access
    /// </summary>
    public bool RestrictFileAccess { get; set; } = false;
    
    /// <summary>
    /// UI theme preference ("Light" or "Dark"). Defaults to Dark.
    /// </summary>
    public string Theme { get; set; } = "Dark";
    
    /// <summary>
    /// Whether the Saved Flows expander is expanded in the UI
    /// </summary>
    public bool SavedFlowsExpanded { get; set; } = true;
    
    /// <summary>
    /// The last used search text for filtering Saved Flows
    /// </summary>
    public string SavedFlowsSearchText { get; set; } = string.Empty;

    /// <summary>
    /// Connection string for the application database.
    /// </summary>
    public string DatabaseConnectionString { get; set; } = $"Data Source={Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Loco", "loco.db")}";
    
    /// <summary>
    /// Path to the settings file
    /// </summary>
    private static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Loco", "settings.json");
    
    /// <summary>
    /// Loads user settings from file
    /// </summary>
    /// <returns>User settings</returns>
    public static UserSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<UserSettings>(json) ?? new UserSettings();
            }
        }
        catch
        {
            // If there's an error loading settings, return default settings
        }
        
        return new UserSettings();
    }
    
    /// <summary>
    /// Saves user settings to file
    /// </summary>
    public void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(SettingsPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // If there's an error saving settings, ignore it
        }
    }
}
