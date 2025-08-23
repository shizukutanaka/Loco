using System;
using System.Timers;
using Loco.UI.Models;

namespace Loco.UI.Services;

/// <summary>
/// Service for managing user settings with auto-save functionality
/// </summary>
public class SettingsService : IDisposable
{
    private readonly UserSettings _settings;
    private readonly Timer _autoSaveTimer;
    private bool _isDirty;
    
    /// <summary>
    /// Creates a new settings service
    /// </summary>
    public SettingsService()
    {
        _settings = UserSettings.Load();
        
        // Set up auto-save timer (saves every 5 seconds if there are changes)
        _autoSaveTimer = new Timer(5000);
        _autoSaveTimer.Elapsed += OnAutoSaveTimerElapsed;
        _autoSaveTimer.AutoReset = true;
        _autoSaveTimer.Start();
    }
    
    /// <summary>
    /// Gets or sets the selected LLM model
    /// </summary>
    public string SelectedModel
    {
        get => _settings.SelectedModel;
        set
        {
            if (_settings.SelectedModel != value)
            {
                _settings.SelectedModel = value;
                MarkAsDirty();
            }
        }
    }
    
    /// <summary>
    /// Gets or sets the selected LLM model ID (stable identifier)
    /// </summary>
    public string SelectedModelId
    {
        get => _settings.SelectedModelId;
        set
        {
            if (_settings.SelectedModelId != value)
            {
                _settings.SelectedModelId = value;
                MarkAsDirty();
            }
        }
    }
    
    /// <summary>
    /// Gets or sets whether to automatically start flows on startup
    /// </summary>
    public bool AutoStartFlows
    {
        get => _settings.AutoStartFlows;
        set
        {
            if (_settings.AutoStartFlows != value)
            {
                _settings.AutoStartFlows = value;
                MarkAsDirty();
            }
        }
    }
    
    /// <summary>
    /// Gets or sets whether to show notifications on errors
    /// </summary>
    public bool ShowErrorNotifications
    {
        get => _settings.ShowErrorNotifications;
        set
        {
            if (_settings.ShowErrorNotifications != value)
            {
                _settings.ShowErrorNotifications = value;
                MarkAsDirty();
            }
        }
    }
    
    /// <summary>
    /// Gets or sets whether to record detailed logs
    /// </summary>
    public bool RecordDetailedLogs
    {
        get => _settings.RecordDetailedLogs;
        set
        {
            if (_settings.RecordDetailedLogs != value)
            {
                _settings.RecordDetailedLogs = value;
                MarkAsDirty();
            }
        }
    }
    
    /// <summary>
    /// Gets or sets whether to enable sandbox execution
    /// </summary>
    public bool EnableSandboxExecution
    {
        get => _settings.EnableSandboxExecution;
        set
        {
            if (_settings.EnableSandboxExecution != value)
            {
                _settings.EnableSandboxExecution = value;
                MarkAsDirty();
            }
        }
    }
    
    /// <summary>
    /// Gets or sets whether to restrict network access
    /// </summary>
    public bool RestrictNetworkAccess
    {
        get => _settings.RestrictNetworkAccess;
        set
        {
            if (_settings.RestrictNetworkAccess != value)
            {
                _settings.RestrictNetworkAccess = value;
                MarkAsDirty();
            }
        }
    }
    
    /// <summary>
    /// Gets or sets whether to restrict file access
    /// </summary>
    public bool RestrictFileAccess
    {
        get => _settings.RestrictFileAccess;
        set
        {
            if (_settings.RestrictFileAccess != value)
            {
                _settings.RestrictFileAccess = value;
                MarkAsDirty();
            }
        }
    }
    
    /// <summary>
    /// Gets or sets the UI theme preference ("Light" or "Dark")
    /// </summary>
    public string Theme
    {
        get => _settings.Theme;
        set
        {
            if (!string.Equals(_settings.Theme, value, StringComparison.OrdinalIgnoreCase))
            {
                _settings.Theme = value;
                MarkAsDirty();
            }
        }
    }
    
    /// <summary>
    /// Gets or sets whether the Saved Flows expander is expanded
    /// </summary>
    public bool SavedFlowsExpanded
    {
        get => _settings.SavedFlowsExpanded;
        set
        {
            if (_settings.SavedFlowsExpanded != value)
            {
                _settings.SavedFlowsExpanded = value;
                MarkAsDirty();
            }
        }
    }
    
    /// <summary>
    /// Gets or sets the last used search text for Saved Flows
    /// </summary>
    public string SavedFlowsSearchText
    {
        get => _settings.SavedFlowsSearchText;
        set
        {
            if (_settings.SavedFlowsSearchText != value)
            {
                _settings.SavedFlowsSearchText = value ?? string.Empty;
                MarkAsDirty();
            }
        }
    }
    
    /// <summary>
    /// Marks the settings as dirty (changed) to trigger auto-save
    /// </summary>
    private void MarkAsDirty()
    {
        _isDirty = true;
    }
    
    /// <summary>
    /// Auto-save timer elapsed handler
    /// </summary>
    private void OnAutoSaveTimerElapsed(object sender, ElapsedEventArgs e)
    {
        if (_isDirty)
        {
            SaveSettings();
        }
    }
    
    /// <summary>
    /// Saves the settings immediately
    /// </summary>
    public void SaveSettings()
    {
        _settings.Save();
        _isDirty = false;
    }
    
    /// <summary>
    /// Disposes the settings service
    /// </summary>
    public void Dispose()
    {
        // Save any pending changes before disposing
        if (_isDirty)
        {
            SaveSettings();
        }
        
        _autoSaveTimer.Stop();
        _autoSaveTimer.Dispose();
    }
}
