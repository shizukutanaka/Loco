using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Loco.Core.Platform;

/// <summary>
/// パッケージマネージャー統合
/// Package manager integration (Winget/Chocolatey/Homebrew/apt/dnf)
///
/// 機能: OS固有のパッケージマネージャーの統一インターフェース
/// Features: Unified interface for OS-specific package managers
/// </summary>
public class PackageManagerIntegration
{
    private readonly CrossPlatformPathHandler.PlatformType _platform;
    private readonly CrossPlatformShellIntegration _shell;
    private readonly PackageManagerType _primaryManager;

    public enum PackageManagerType
    {
        Winget,      // Windows Package Manager
        Chocolatey,  // Windows Chocolatey
        Scoop,       // Windows Scoop
        Homebrew,    // macOS/Linux Homebrew
        Apt,         // Debian/Ubuntu apt
        Dnf,         // Fedora/RHEL dnf
        Yum,         // Legacy RHEL yum
        Pacman,      // Arch Linux pacman
        Zypper,      // openSUSE zypper
        Unknown
    }

    public PackageManagerIntegration()
    {
        _platform = CrossPlatformPathHandler.DetectPlatform();
        _shell = new CrossPlatformShellIntegration();
        _primaryManager = DetectPrimaryPackageManager();
    }

    public PackageManagerType PrimaryManager => _primaryManager;

    /// <summary>
    /// プライマリパッケージマネージャーを検出
    /// Detect primary package manager
    /// </summary>
    public static PackageManagerType DetectPrimaryPackageManager()
    {
        var platform = CrossPlatformPathHandler.DetectPlatform();

        switch (platform)
        {
            case CrossPlatformPathHandler.PlatformType.Windows:
                // Check for winget first (built-in to Windows 11+)
                if (IsPackageManagerAvailable(PackageManagerType.Winget))
                {
                    return PackageManagerType.Winget;
                }
                // Then chocolatey
                if (IsPackageManagerAvailable(PackageManagerType.Chocolatey))
                {
                    return PackageManagerType.Chocolatey;
                }
                // Then scoop
                if (IsPackageManagerAvailable(PackageManagerType.Scoop))
                {
                    return PackageManagerType.Scoop;
                }
                return PackageManagerType.Unknown;

            case CrossPlatformPathHandler.PlatformType.MacOS:
                // macOS uses Homebrew
                if (IsPackageManagerAvailable(PackageManagerType.Homebrew))
                {
                    return PackageManagerType.Homebrew;
                }
                return PackageManagerType.Unknown;

            case CrossPlatformPathHandler.PlatformType.Linux:
                // Check for common Linux package managers
                if (IsPackageManagerAvailable(PackageManagerType.Apt))
                {
                    return PackageManagerType.Apt;
                }
                if (IsPackageManagerAvailable(PackageManagerType.Dnf))
                {
                    return PackageManagerType.Dnf;
                }
                if (IsPackageManagerAvailable(PackageManagerType.Pacman))
                {
                    return PackageManagerType.Pacman;
                }
                if (IsPackageManagerAvailable(PackageManagerType.Zypper))
                {
                    return PackageManagerType.Zypper;
                }
                if (IsPackageManagerAvailable(PackageManagerType.Yum))
                {
                    return PackageManagerType.Yum;
                }
                if (IsPackageManagerAvailable(PackageManagerType.Homebrew))
                {
                    return PackageManagerType.Homebrew;
                }
                return PackageManagerType.Unknown;

            default:
                return PackageManagerType.Unknown;
        }
    }

    private static bool IsPackageManagerAvailable(PackageManagerType manager)
    {
        var command = GetPackageManagerCommand(manager);
        var shell = new CrossPlatformShellIntegration();

        try
        {
            var result = shell.ExecuteCommandAsync(
                $"command -v {command} || where {command} || which {command}",
                timeoutMs: 2000
            ).Result;

            return result.Success || !string.IsNullOrWhiteSpace(result.StandardOutput);
        }
        catch
        {
            return false;
        }
    }

    private static string GetPackageManagerCommand(PackageManagerType manager)
    {
        return manager switch
        {
            PackageManagerType.Winget => "winget",
            PackageManagerType.Chocolatey => "choco",
            PackageManagerType.Scoop => "scoop",
            PackageManagerType.Homebrew => "brew",
            PackageManagerType.Apt => "apt",
            PackageManagerType.Dnf => "dnf",
            PackageManagerType.Yum => "yum",
            PackageManagerType.Pacman => "pacman",
            PackageManagerType.Zypper => "zypper",
            _ => "unknown"
        };
    }

    /// <summary>
    /// パッケージをインストール
    /// Install package
    /// </summary>
    public async Task<PackageOperationResult> InstallPackageAsync(
        string packageName,
        PackageManagerType? manager = null,
        bool assumeYes = true)
    {
        var targetManager = manager ?? _primaryManager;
        var command = BuildInstallCommand(packageName, targetManager, assumeYes);

        var result = await _shell.ExecuteCommandAsync(
            command,
            timeoutMs: 300000 // 5 minutes timeout
        );

        return new PackageOperationResult
        {
            Success = result.Success,
            PackageName = packageName,
            Operation = "Install",
            Manager = targetManager,
            Output = result.StandardOutput,
            Error = result.StandardError,
            ExitCode = result.ExitCode
        };
    }

    /// <summary>
    /// パッケージをアンインストール
    /// Uninstall package
    /// </summary>
    public async Task<PackageOperationResult> UninstallPackageAsync(
        string packageName,
        PackageManagerType? manager = null,
        bool assumeYes = true)
    {
        var targetManager = manager ?? _primaryManager;
        var command = BuildUninstallCommand(packageName, targetManager, assumeYes);

        var result = await _shell.ExecuteCommandAsync(
            command,
            timeoutMs: 300000
        );

        return new PackageOperationResult
        {
            Success = result.Success,
            PackageName = packageName,
            Operation = "Uninstall",
            Manager = targetManager,
            Output = result.StandardOutput,
            Error = result.StandardError,
            ExitCode = result.ExitCode
        };
    }

    /// <summary>
    /// パッケージを更新
    /// Update package
    /// </summary>
    public async Task<PackageOperationResult> UpdatePackageAsync(
        string packageName,
        PackageManagerType? manager = null,
        bool assumeYes = true)
    {
        var targetManager = manager ?? _primaryManager;
        var command = BuildUpdateCommand(packageName, targetManager, assumeYes);

        var result = await _shell.ExecuteCommandAsync(
            command,
            timeoutMs: 300000
        );

        return new PackageOperationResult
        {
            Success = result.Success,
            PackageName = packageName,
            Operation = "Update",
            Manager = targetManager,
            Output = result.StandardOutput,
            Error = result.StandardError,
            ExitCode = result.ExitCode
        };
    }

    /// <summary>
    /// パッケージを検索
    /// Search for package
    /// </summary>
    public async Task<List<PackageInfo>> SearchPackageAsync(
        string searchTerm,
        PackageManagerType? manager = null)
    {
        var targetManager = manager ?? _primaryManager;
        var command = BuildSearchCommand(searchTerm, targetManager);

        var result = await _shell.ExecuteCommandAsync(
            command,
            timeoutMs: 30000
        );

        if (!result.Success)
        {
            return new List<PackageInfo>();
        }

        return ParseSearchResults(result.StandardOutput, targetManager);
    }

    /// <summary>
    /// インストールされているパッケージ一覧を取得
    /// List installed packages
    /// </summary>
    public async Task<List<PackageInfo>> ListInstalledPackagesAsync(
        PackageManagerType? manager = null)
    {
        var targetManager = manager ?? _primaryManager;
        var command = BuildListCommand(targetManager);

        var result = await _shell.ExecuteCommandAsync(
            command,
            timeoutMs: 30000
        );

        if (!result.Success)
        {
            return new List<PackageInfo>();
        }

        return ParseListResults(result.StandardOutput, targetManager);
    }

    private string BuildInstallCommand(string packageName, PackageManagerType manager, bool assumeYes)
    {
        return manager switch
        {
            PackageManagerType.Winget => $"winget install {packageName} --silent --accept-package-agreements --accept-source-agreements",
            PackageManagerType.Chocolatey => assumeYes ? $"choco install {packageName} -y" : $"choco install {packageName}",
            PackageManagerType.Scoop => $"scoop install {packageName}",
            PackageManagerType.Homebrew => $"brew install {packageName}",
            PackageManagerType.Apt => assumeYes ? $"sudo apt install {packageName} -y" : $"sudo apt install {packageName}",
            PackageManagerType.Dnf => assumeYes ? $"sudo dnf install {packageName} -y" : $"sudo dnf install {packageName}",
            PackageManagerType.Yum => assumeYes ? $"sudo yum install {packageName} -y" : $"sudo yum install {packageName}",
            PackageManagerType.Pacman => $"sudo pacman -S {packageName} --noconfirm",
            PackageManagerType.Zypper => assumeYes ? $"sudo zypper install {packageName} -y" : $"sudo zypper install {packageName}",
            _ => $"echo 'Unknown package manager'"
        };
    }

    private string BuildUninstallCommand(string packageName, PackageManagerType manager, bool assumeYes)
    {
        return manager switch
        {
            PackageManagerType.Winget => $"winget uninstall {packageName} --silent",
            PackageManagerType.Chocolatey => assumeYes ? $"choco uninstall {packageName} -y" : $"choco uninstall {packageName}",
            PackageManagerType.Scoop => $"scoop uninstall {packageName}",
            PackageManagerType.Homebrew => $"brew uninstall {packageName}",
            PackageManagerType.Apt => assumeYes ? $"sudo apt remove {packageName} -y" : $"sudo apt remove {packageName}",
            PackageManagerType.Dnf => assumeYes ? $"sudo dnf remove {packageName} -y" : $"sudo dnf remove {packageName}",
            PackageManagerType.Yum => assumeYes ? $"sudo yum remove {packageName} -y" : $"sudo yum remove {packageName}",
            PackageManagerType.Pacman => $"sudo pacman -R {packageName} --noconfirm",
            PackageManagerType.Zypper => assumeYes ? $"sudo zypper remove {packageName} -y" : $"sudo zypper remove {packageName}",
            _ => $"echo 'Unknown package manager'"
        };
    }

    private string BuildUpdateCommand(string packageName, PackageManagerType manager, bool assumeYes)
    {
        return manager switch
        {
            PackageManagerType.Winget => $"winget upgrade {packageName} --silent --accept-package-agreements --accept-source-agreements",
            PackageManagerType.Chocolatey => assumeYes ? $"choco upgrade {packageName} -y" : $"choco upgrade {packageName}",
            PackageManagerType.Scoop => $"scoop update {packageName}",
            PackageManagerType.Homebrew => $"brew upgrade {packageName}",
            PackageManagerType.Apt => assumeYes ? $"sudo apt upgrade {packageName} -y" : $"sudo apt upgrade {packageName}",
            PackageManagerType.Dnf => assumeYes ? $"sudo dnf update {packageName} -y" : $"sudo dnf update {packageName}",
            PackageManagerType.Yum => assumeYes ? $"sudo yum update {packageName} -y" : $"sudo yum update {packageName}",
            PackageManagerType.Pacman => $"sudo pacman -Syu {packageName} --noconfirm",
            PackageManagerType.Zypper => assumeYes ? $"sudo zypper update {packageName} -y" : $"sudo zypper update {packageName}",
            _ => $"echo 'Unknown package manager'"
        };
    }

    private string BuildSearchCommand(string searchTerm, PackageManagerType manager)
    {
        return manager switch
        {
            PackageManagerType.Winget => $"winget search {searchTerm}",
            PackageManagerType.Chocolatey => $"choco search {searchTerm}",
            PackageManagerType.Scoop => $"scoop search {searchTerm}",
            PackageManagerType.Homebrew => $"brew search {searchTerm}",
            PackageManagerType.Apt => $"apt search {searchTerm}",
            PackageManagerType.Dnf => $"dnf search {searchTerm}",
            PackageManagerType.Yum => $"yum search {searchTerm}",
            PackageManagerType.Pacman => $"pacman -Ss {searchTerm}",
            PackageManagerType.Zypper => $"zypper search {searchTerm}",
            _ => $"echo 'Unknown package manager'"
        };
    }

    private string BuildListCommand(PackageManagerType manager)
    {
        return manager switch
        {
            PackageManagerType.Winget => "winget list",
            PackageManagerType.Chocolatey => "choco list --local-only",
            PackageManagerType.Scoop => "scoop list",
            PackageManagerType.Homebrew => "brew list",
            PackageManagerType.Apt => "dpkg -l",
            PackageManagerType.Dnf => "dnf list installed",
            PackageManagerType.Yum => "yum list installed",
            PackageManagerType.Pacman => "pacman -Q",
            PackageManagerType.Zypper => "zypper packages --installed-only",
            _ => "echo 'Unknown package manager'"
        };
    }

    private List<PackageInfo> ParseSearchResults(string output, PackageManagerType manager)
    {
        // Simplified parsing - would need more robust implementation
        var packages = new List<PackageInfo>();
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines.Skip(2)) // Skip header lines
        {
            var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                packages.Add(new PackageInfo
                {
                    Name = parts[0],
                    Version = parts.Length > 1 ? parts[1] : "Unknown",
                    Manager = manager,
                    IsInstalled = false
                });
            }
        }

        return packages;
    }

    private List<PackageInfo> ParseListResults(string output, PackageManagerType manager)
    {
        // Simplified parsing
        var packages = new List<PackageInfo>();
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines.Skip(2))
        {
            var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                packages.Add(new PackageInfo
                {
                    Name = parts[0],
                    Version = parts.Length > 1 ? parts[1] : "Unknown",
                    Manager = manager,
                    IsInstalled = true
                });
            }
        }

        return packages;
    }

    /// <summary>
    /// パッケージマネージャー情報を取得
    /// Get package manager information
    /// </summary>
    public PackageManagerInfo GetPackageManagerInfo(PackageManagerType? manager = null)
    {
        var targetManager = manager ?? _primaryManager;
        return new PackageManagerInfo
        {
            Type = targetManager,
            Name = GetPackageManagerName(targetManager),
            Command = GetPackageManagerCommand(targetManager),
            IsAvailable = IsPackageManagerAvailable(targetManager),
            Platform = _platform,
            Description = GetPackageManagerDescription(targetManager)
        };
    }

    private string GetPackageManagerName(PackageManagerType manager)
    {
        return manager switch
        {
            PackageManagerType.Winget => "Windows Package Manager",
            PackageManagerType.Chocolatey => "Chocolatey",
            PackageManagerType.Scoop => "Scoop",
            PackageManagerType.Homebrew => "Homebrew",
            PackageManagerType.Apt => "APT (Advanced Package Tool)",
            PackageManagerType.Dnf => "DNF (Dandified YUM)",
            PackageManagerType.Yum => "YUM (Yellowdog Updater Modified)",
            PackageManagerType.Pacman => "Pacman",
            PackageManagerType.Zypper => "Zypper",
            _ => "Unknown"
        };
    }

    private string GetPackageManagerDescription(PackageManagerType manager)
    {
        return manager switch
        {
            PackageManagerType.Winget => "Built-in package manager for Windows 10/11",
            PackageManagerType.Chocolatey => "Popular third-party package manager for Windows",
            PackageManagerType.Scoop => "Command-line installer for Windows",
            PackageManagerType.Homebrew => "The missing package manager for macOS (and Linux)",
            PackageManagerType.Apt => "Debian/Ubuntu package management system",
            PackageManagerType.Dnf => "Next-generation package manager for Fedora/RHEL",
            PackageManagerType.Yum => "Legacy package manager for RHEL-based distributions",
            PackageManagerType.Pacman => "Package manager for Arch Linux",
            PackageManagerType.Zypper => "Package manager for openSUSE",
            _ => "Unknown package manager"
        };
    }

    public class PackageOperationResult
    {
        public bool Success { get; set; }
        public string PackageName { get; set; } = string.Empty;
        public string Operation { get; set; } = string.Empty;
        public PackageManagerType Manager { get; set; }
        public string Output { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
        public int ExitCode { get; set; }
    }

    public class PackageInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public PackageManagerType Manager { get; set; }
        public bool IsInstalled { get; set; }
    }

    public class PackageManagerInfo
    {
        public PackageManagerType Type { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Command { get; set; } = string.Empty;
        public bool IsAvailable { get; set; }
        public CrossPlatformPathHandler.PlatformType Platform { get; set; }
        public string Description { get; set; } = string.Empty;
    }
}
