using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Win32;
using Microsoft.Extensions.Logging;
using Loco.Core.Models;
using Loco.Core.Marketplace;
using Loco.Core.Sync;

namespace Loco.Core.Installer;

/// <summary>
/// Protocol handler for loco:// URLs
/// Following John Carmack's direct approach
/// </summary>
public class ProtocolHandler
{
    private readonly ILogger<ProtocolHandler> _logger;
    private readonly FlowMarketplace _marketplace;
    private readonly string _executablePath;
    
    public ProtocolHandler(ILogger<ProtocolHandler> logger, FlowMarketplace marketplace)
    {
        _logger = logger;
        _marketplace = marketplace;
        _executablePath = Process.GetCurrentProcess().MainModule?.FileName ?? "loco.exe";
    }
    
    /// <summary>
    /// Register loco:// protocol handler
    /// </summary>
    public bool RegisterProtocol()
    {
        try
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _logger.LogWarning("Protocol registration is only supported on Windows");
                return false;
            }
            
            // Check if running as administrator
            if (!IsAdministrator())
            {
                _logger.LogWarning("Administrator privileges required for protocol registration");
                
                // Try to restart as administrator
                var startInfo = new ProcessStartInfo
                {
                    FileName = _executablePath,
                    Arguments = "--register-protocol",
                    UseShellExecute = true,
                    Verb = "runas"
                };
                
                try
                {
                    Process.Start(startInfo);
                    return true;
                }
                catch
                {
                    return false;
                }
            }
            
            // Register protocol in registry
            using (var key = Registry.ClassesRoot.CreateSubKey("loco"))
            {
                key.SetValue("", "URL:Loco Protocol");
                key.SetValue("URL Protocol", "");
                
                using (var iconKey = key.CreateSubKey("DefaultIcon"))
                {
                    iconKey.SetValue("", $"\"{_executablePath}\",1");
                }
                
                using (var commandKey = key.CreateSubKey(@"shell\open\command"))
                {
                    commandKey.SetValue("", $"\"{_executablePath}\" \"%1\"");
                }
            }
            
            _logger.LogInformation("Protocol handler registered successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register protocol handler");
            return false;
        }
    }
    
    /// <summary>
    /// Unregister protocol handler
    /// </summary>
    public bool UnregisterProtocol()
    {
        try
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return false;
            
            if (!IsAdministrator())
                return false;
            
            Registry.ClassesRoot.DeleteSubKeyTree("loco", false);
            
            _logger.LogInformation("Protocol handler unregistered");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unregister protocol handler");
            return false;
        }
    }
    
    /// <summary>
    /// Handle protocol URL
    /// </summary>
    public async Task<bool> HandleUrlAsync(string url)
    {
        try
        {
            _logger.LogInformation("Handling URL: {Url}", url);
            
            var uri = new Uri(url);
            var action = uri.Host.ToLower();
            var path = uri.LocalPath.TrimStart('/');
            
            switch (action)
            {
                case "install":
                    return await HandleInstallAsync(path);
                    
                case "share":
                    return await HandleShareAsync(path);
                    
                case "subscribe":
                    return await HandleSubscribeAsync(path);
                    
                case "import":
                    return await HandleImportAsync(path);
                    
                case "open":
                    return HandleOpen(path);
                    
                default:
                    _logger.LogWarning("Unknown action: {Action}", action);
                    return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle URL: {Url}", url);
            return false;
        }
    }
    
    /// <summary>
    /// Handle install action
    /// </summary>
    private async Task<bool> HandleInstallAsync(string flowId)
    {
        try
        {
            _logger.LogInformation("Installing flow: {FlowId}", flowId);
            
            var result = await _marketplace.InstallAsync(flowId);
            
            if (result.Success)
            {
                ShowNotification("Flow Installed", $"{result.Flow?.Name ?? flowId} has been installed successfully");
                
                // Open flow in editor if available
                if (!string.IsNullOrEmpty(result.LocalPath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = result.LocalPath,
                        UseShellExecute = true
                    });
                }
                
                return true;
            }
            else
            {
                ShowNotification("Installation Failed", result.Message);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to install flow: {FlowId}", flowId);
            ShowNotification("Installation Error", ex.Message);
            return false;
        }
    }
    
    /// <summary>
    /// Handle share action
    /// </summary>
    private async Task<bool> HandleShareAsync(string flowId)
    {
        try
        {
            _logger.LogInformation("Sharing flow: {FlowId}", flowId);
            
            // Get flow from local storage
            var flowPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Loco", "Flows", $"{flowId}.json");
            
            if (!File.Exists(flowPath))
            {
                ShowNotification("Flow Not Found", $"Flow {flowId} not found locally");
                return false;
            }
            
            var json = await File.ReadAllTextAsync(flowPath);
            var flow = JsonSerializer.Deserialize<FlowDefinition>(json);
            
            if (flow == null)
                return false;
            
            // Share via marketplace
            var metadata = new MarketplaceMetadata
            {
                Author = Environment.UserName,
                Category = MarketplaceCategory.Other,
                Tags = new[] { "shared" }
            };
            
            var result = await _marketplace.ShareAsync(flow, metadata);
            
            if (result.Success)
            {
                ShowNotification("Flow Shared", $"Share URL: {result.ShareUrl}");
                
                // Copy to clipboard if possible
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "cmd",
                        Arguments = $"/c echo {result.ShareUrl} | clip",
                        WindowStyle = ProcessWindowStyle.Hidden
                    });
                }
                
                return true;
            }
            else
            {
                ShowNotification("Share Failed", result.Message);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to share flow: {FlowId}", flowId);
            return false;
        }
    }
    
    /// <summary>
    /// Handle subscribe action
    /// </summary>
    private async Task<bool> HandleSubscribeAsync(string flowId)
    {
        try
        {
            _logger.LogInformation("Subscribing to flow: {FlowId}", flowId);
            
            // Add to subscribed flows
            var optionsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Loco", "sync_options.json");
            
            FlowSyncOptions options;
            if (File.Exists(optionsPath))
            {
                var json = await File.ReadAllTextAsync(optionsPath);
                options = JsonSerializer.Deserialize<FlowSyncOptions>(json) ?? new FlowSyncOptions();
            }
            else
            {
                options = new FlowSyncOptions();
            }
            
            if (!options.SubscribedFlows.Contains(flowId))
            {
                options.SubscribedFlows.Add(flowId);
                
                var updatedJson = JsonSerializer.Serialize(options);
                await File.WriteAllTextAsync(optionsPath, updatedJson);
                
                ShowNotification("Subscribed", $"You will receive updates for {flowId}");
                return true;
            }
            else
            {
                ShowNotification("Already Subscribed", $"You are already subscribed to {flowId}");
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to subscribe to flow: {FlowId}", flowId);
            return false;
        }
    }
    
    /// <summary>
    /// Handle import action
    /// </summary>
    private async Task<bool> HandleImportAsync(string path)
    {
        try
        {
            _logger.LogInformation("Importing from: {Path}", path);
            
            // Decode base64 if needed
            if (!path.Contains("/") && !path.Contains("\\"))
            {
                try
                {
                    var decoded = Convert.FromBase64String(path);
                    path = System.Text.Encoding.UTF8.GetString(decoded);
                }
                catch { }
            }
            
            // Import from URL or file
            if (path.StartsWith("http"))
            {
                var result = await _marketplace.InstallFromUrlAsync(path);
                if (result.Success)
                {
                    ShowNotification("Flow Imported", $"{result.Flow?.Name} imported successfully");
                    return true;
                }
            }
            
            ShowNotification("Import Failed", "Unable to import flow");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import: {Path}", path);
            return false;
        }
    }
    
    /// <summary>
    /// Handle open action
    /// </summary>
    private bool HandleOpen(string flowId)
    {
        try
        {
            _logger.LogInformation("Opening flow: {FlowId}", flowId);
            
            var flowPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Loco", "Flows", $"{flowId}.json");
            
            if (File.Exists(flowPath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = flowPath,
                    UseShellExecute = true
                });
                return true;
            }
            
            ShowNotification("Flow Not Found", $"Flow {flowId} not found");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open flow: {FlowId}", flowId);
            return false;
        }
    }
    
    /// <summary>
    /// Show notification (Windows Toast or Console)
    /// </summary>
    private void ShowNotification(string title, string message)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Use Windows toast notification
                var toastXml = $@"
<toast>
    <visual>
        <binding template='ToastGeneric'>
            <text>{title}</text>
            <text>{message}</text>
        </binding>
    </visual>
</toast>";
                
                // In production, use Windows.UI.Notifications
                _logger.LogInformation("[{Title}] {Message}", title, message);
            }
            else
            {
                _logger.LogInformation("[{Title}] {Message}", title, message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to show notification");
        }
    }
    
    /// <summary>
    /// Check if running as administrator
    /// </summary>
    private bool IsAdministrator()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return false;
        
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
}

/// <summary>
/// One-click installer
/// </summary>
public class OneClickInstaller
{
    private readonly ILogger<OneClickInstaller> _logger;
    private readonly ProtocolHandler _protocolHandler;
    
    public OneClickInstaller(ILogger<OneClickInstaller> logger, ProtocolHandler protocolHandler)
    {
        _logger = logger;
        _protocolHandler = protocolHandler;
    }
    
    /// <summary>
    /// Install Loco with protocol handler
    /// </summary>
    public async Task<bool> InstallAsync()
    {
        try
        {
            _logger.LogInformation("Starting Loco installation");
            
            // 1. Create directories
            var appDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Loco");
            
            Directory.CreateDirectory(appDir);
            Directory.CreateDirectory(Path.Combine(appDir, "Flows"));
            Directory.CreateDirectory(Path.Combine(appDir, "Marketplace"));
            Directory.CreateDirectory(Path.Combine(appDir, "Logs"));
            
            // 2. Register protocol handler
            _protocolHandler.RegisterProtocol();
            
            // 3. Create desktop shortcut
            CreateDesktopShortcut();
            
            // 4. Create start menu entry
            CreateStartMenuEntry();
            
            // 5. Set up auto-start (optional)
            if (await PromptAutoStartAsync())
            {
                SetupAutoStart();
            }
            
            _logger.LogInformation("Installation completed successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Installation failed");
            return false;
        }
    }
    
    /// <summary>
    /// Uninstall Loco
    /// </summary>
    public bool Uninstall()
    {
        try
        {
            _logger.LogInformation("Starting Loco uninstallation");
            
            // 1. Unregister protocol handler
            _protocolHandler.UnregisterProtocol();
            
            // 2. Remove shortcuts
            RemoveDesktopShortcut();
            RemoveStartMenuEntry();
            
            // 3. Remove auto-start
            RemoveAutoStart();
            
            // 4. Optional: Remove app data
            if (PromptRemoveData())
            {
                var appDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Loco");
                
                if (Directory.Exists(appDir))
                {
                    Directory.Delete(appDir, true);
                }
            }
            
            _logger.LogInformation("Uninstallation completed");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Uninstallation failed");
            return false;
        }
    }
    
    private void CreateDesktopShortcut()
    {
        // Implementation for creating desktop shortcut
        // Use Windows Script Host or IShellLink COM interface
    }
    
    private void CreateStartMenuEntry()
    {
        // Implementation for creating start menu entry
    }
    
    private void SetupAutoStart()
    {
        // Add to Windows startup registry or Task Scheduler
    }
    
    private void RemoveDesktopShortcut()
    {
        // Remove desktop shortcut
    }
    
    private void RemoveStartMenuEntry()
    {
        // Remove start menu entry
    }
    
    private void RemoveAutoStart()
    {
        // Remove from startup
    }
    
    private async Task<bool> PromptAutoStartAsync()
    {
        // In GUI app, show dialog
        // In console app, prompt user
        await Task.Delay(1);
        return false;
    }
    
    private bool PromptRemoveData()
    {
        // Prompt user to remove app data
        return false;
    }
}
