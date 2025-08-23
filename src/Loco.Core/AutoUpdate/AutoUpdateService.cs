using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Loco.Core.AutoUpdate;

/// <summary>
/// Auto-update service for Loco
/// Following John Carmack's principle: keep it simple and efficient
/// </summary>
public class AutoUpdateService : BackgroundService
{
    private readonly ILogger<AutoUpdateService> _logger;
    private readonly HttpClient _httpClient;
    private readonly AutoUpdateSettings _settings;
    private readonly string _currentVersion;
    private readonly string _updateCheckUrl;

    public AutoUpdateService(ILogger<AutoUpdateService> logger, HttpClient httpClient, AutoUpdateSettings settings)
    {
        _logger = logger;
        _httpClient = httpClient;
        _settings = settings;
        _currentVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.1";
        _updateCheckUrl = "https://api.github.com/repos/shizukutanaka/Loco/releases/latest";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.Enabled)
        {
            _logger.LogInformation("Auto-update is disabled");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckForUpdatesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking for updates");
            }

            // Check every configured interval
            await Task.Delay(TimeSpan.FromHours(_settings.CheckIntervalHours), stoppingToken);
        }
    }

    /// <summary>
    /// Check for available updates
    /// </summary>
    public async Task<UpdateInfo> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Checking for updates...");

            // Get latest release from GitHub
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Loco-AutoUpdater");
            var response = await _httpClient.GetAsync(_updateCheckUrl, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to check for updates: {StatusCode}", response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var release = JsonSerializer.Deserialize<GitHubRelease>(json);

            if (release == null)
            {
                return null;
            }

            var latestVersion = release.tag_name.TrimStart('v');
            var hasUpdate = IsNewerVersion(latestVersion, _currentVersion);

            var updateInfo = new UpdateInfo
            {
                CurrentVersion = _currentVersion,
                LatestVersion = latestVersion,
                HasUpdate = hasUpdate,
                ReleaseNotes = release.body,
                ReleaseUrl = release.html_url,
                PublishedAt = release.published_at
            };

            if (hasUpdate)
            {
                _logger.LogInformation("New version available: {Version}", latestVersion);
                
                // Find appropriate download URL
                var asset = release.assets?.FirstOrDefault(a => 
                    a.name.Contains("x64", StringComparison.OrdinalIgnoreCase) &&
                    (a.name.EndsWith(".msi") || a.name.EndsWith(".zip")));
                
                if (asset != null)
                {
                    updateInfo.DownloadUrl = asset.browser_download_url;
                    updateInfo.DownloadSize = asset.size;
                }

                // Notify user if configured
                if (_settings.NotifyOnUpdate)
                {
                    await NotifyUpdateAvailableAsync(updateInfo);
                }

                // Auto-download if configured
                if (_settings.AutoDownload && !string.IsNullOrEmpty(updateInfo.DownloadUrl))
                {
                    await DownloadUpdateAsync(updateInfo, cancellationToken);
                }
            }
            else
            {
                _logger.LogInformation("You are running the latest version");
            }

            return updateInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check for updates");
            return null;
        }
    }

    /// <summary>
    /// Download update package
    /// </summary>
    private async Task<string> DownloadUpdateAsync(UpdateInfo updateInfo, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(updateInfo.DownloadUrl))
        {
            throw new InvalidOperationException("Download URL is not available");
        }

        var fileName = Path.GetFileName(new Uri(updateInfo.DownloadUrl).LocalPath);
        var downloadPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Loco", "Updates", updateInfo.LatestVersion);
        
        Directory.CreateDirectory(downloadPath);
        
        var filePath = Path.Combine(downloadPath, fileName);

        // Check if already downloaded
        if (File.Exists(filePath))
        {
            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length == updateInfo.DownloadSize)
            {
                _logger.LogInformation("Update already downloaded: {Path}", filePath);
                return filePath;
            }
        }

        _logger.LogInformation("Downloading update: {Url}", updateInfo.DownloadUrl);

        // Download with progress
        using var response = await _httpClient.GetAsync(updateInfo.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1L;
        var canReportProgress = totalBytes != -1;

        using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);
        
        var buffer = new byte[8192];
        var totalRead = 0L;
        var read = 0;

        while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
        {
            await fileStream.WriteAsync(buffer, 0, read, cancellationToken);
            totalRead += read;

            if (canReportProgress)
            {
                var progress = (double)totalRead / totalBytes * 100;
                _logger.LogDebug("Download progress: {Progress:F1}%", progress);
            }
        }

        _logger.LogInformation("Update downloaded successfully: {Path}", filePath);

        // Auto-install if configured
        if (_settings.AutoInstall)
        {
            await InstallUpdateAsync(filePath);
        }

        return filePath;
    }

    /// <summary>
    /// Install downloaded update
    /// </summary>
    public async Task InstallUpdateAsync(string updateFilePath)
    {
        if (!File.Exists(updateFilePath))
        {
            throw new FileNotFoundException("Update file not found", updateFilePath);
        }

        _logger.LogInformation("Installing update: {Path}", updateFilePath);

        // Create update script
        var scriptPath = Path.Combine(Path.GetTempPath(), "loco-update.bat");
        var script = $@"
@echo off
echo Waiting for Loco to close...
timeout /t 3 /nobreak > nul

echo Installing update...
if ""{Path.GetExtension(updateFilePath).ToLower()}"" == "".msi"" (
    msiexec /i ""{updateFilePath}"" /qn /norestart
) else (
    echo Extracting update...
    powershell -Command ""Expand-Archive -Path '{updateFilePath}' -DestinationPath '{AppDomain.CurrentDomain.BaseDirectory}' -Force""
)

echo Starting Loco...
start """" ""{Process.GetCurrentProcess().MainModule?.FileName}""

echo Update complete!
del ""%~f0""
";
        await File.WriteAllTextAsync(scriptPath, script);

        // Start update process
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = scriptPath,
                UseShellExecute = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            }
        };

        process.Start();

        // Shutdown application
        _logger.LogInformation("Restarting for update installation...");
        Environment.Exit(0);
    }

    /// <summary>
    /// Notify user about available update
    /// </summary>
    private async Task NotifyUpdateAvailableAsync(UpdateInfo updateInfo)
    {
        // This would typically show a system notification or update UI
        // For now, just log it
        _logger.LogInformation("Update notification: New version {Version} is available", updateInfo.LatestVersion);
        
        // In a real implementation, this would:
        // 1. Show Windows toast notification
        // 2. Update UI status bar
        // 3. Send email if configured
        
        await Task.CompletedTask;
    }

    /// <summary>
    /// Check if version is newer
    /// </summary>
    private bool IsNewerVersion(string latestVersion, string currentVersion)
    {
        try
        {
            var latest = Version.Parse(latestVersion);
            var current = Version.Parse(currentVersion);
            return latest > current;
        }
        catch
        {
            // If parsing fails, do string comparison
            return string.Compare(latestVersion, currentVersion, StringComparison.OrdinalIgnoreCase) > 0;
        }
    }
}

/// <summary>
/// Auto-update settings
/// </summary>
public class AutoUpdateSettings
{
    public bool Enabled { get; set; } = true;
    public bool NotifyOnUpdate { get; set; } = true;
    public bool AutoDownload { get; set; } = false;
    public bool AutoInstall { get; set; } = false;
    public int CheckIntervalHours { get; set; } = 24;
    public string UpdateChannel { get; set; } = "stable"; // stable, beta, nightly
}

/// <summary>
/// Update information
/// </summary>
public class UpdateInfo
{
    public string CurrentVersion { get; set; }
    public string LatestVersion { get; set; }
    public bool HasUpdate { get; set; }
    public string ReleaseNotes { get; set; }
    public string ReleaseUrl { get; set; }
    public string DownloadUrl { get; set; }
    public long DownloadSize { get; set; }
    public DateTime PublishedAt { get; set; }
}

/// <summary>
/// GitHub release model
/// </summary>
public class GitHubRelease
{
    public string tag_name { get; set; }
    public string name { get; set; }
    public string body { get; set; }
    public string html_url { get; set; }
    public DateTime published_at { get; set; }
    public bool prerelease { get; set; }
    public GitHubAsset[] assets { get; set; }
}

/// <summary>
/// GitHub release asset
/// </summary>
public class GitHubAsset
{
    public string name { get; set; }
    public string browser_download_url { get; set; }
    public long size { get; set; }
    public string content_type { get; set; }
}
