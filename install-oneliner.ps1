# Loco One-Line Installer
# Lightweight, fast, and reliable installation

param(
    [string]$Version = "latest",
    [string]$InstallDir = "$env:LOCALAPPDATA\Loco",
    [switch]$AddToPath = $true,
    [switch]$Silent = $false
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

function Write-Status {
    param([string]$Message)
    if (-not $Silent) {
        Write-Host "► $Message" -ForegroundColor Cyan
    }
}

function Write-Success {
    param([string]$Message)
    if (-not $Silent) {
        Write-Host "✓ $Message" -ForegroundColor Green
    }
}

function Write-Error {
    param([string]$Message)
    Write-Host "✗ $Message" -ForegroundColor Red
}

try {
    Write-Status "Installing Loco automation platform..."
    
    # Check Windows version
    $osVersion = [System.Environment]::OSVersion.Version
    if ($osVersion.Major -lt 10) {
        throw "Windows 10 or later is required"
    }
    
    # Check .NET 8 installation
    Write-Status "Checking .NET 8 runtime..."
    $dotnetVersion = & dotnet --list-runtimes 2>$null | Where-Object { $_ -match "Microsoft.NETCore.App 8\." }
    if (-not $dotnetVersion) {
        Write-Status "Installing .NET 8 runtime..."
        $dotnetInstaller = "$env:TEMP\dotnet-install.ps1"
        Invoke-WebRequest -Uri "https://dot.net/v1/dotnet-install.ps1" -OutFile $dotnetInstaller
        & $dotnetInstaller -Runtime dotnet -Version 8.0.0 -InstallDir "$env:ProgramFiles\dotnet"
        $env:Path = "$env:ProgramFiles\dotnet;$env:Path"
    }
    Write-Success ".NET 8 runtime ready"
    
    # Create installation directory
    if (Test-Path $InstallDir) {
        Write-Status "Removing previous installation..."
        Remove-Item -Path $InstallDir -Recurse -Force
    }
    New-Item -Path $InstallDir -ItemType Directory -Force | Out-Null
    Write-Success "Created installation directory"
    
    # Determine download URL
    if ($Version -eq "latest") {
        $apiUrl = "https://api.github.com/repos/loco/loco/releases/latest"
        $release = Invoke-RestMethod -Uri $apiUrl
        $downloadUrl = ($release.assets | Where-Object { $_.name -like "*win-x64*" })[0].browser_download_url
    } else {
        $downloadUrl = "https://github.com/loco/loco/releases/download/$Version/loco-win-x64.zip"
    }
    
    # Download and extract
    Write-Status "Downloading Loco..."
    $zipFile = "$env:TEMP\loco.zip"
    Invoke-WebRequest -Uri $downloadUrl -OutFile $zipFile
    
    Write-Status "Extracting files..."
    Expand-Archive -Path $zipFile -DestinationPath $InstallDir -Force
    Remove-Item -Path $zipFile
    Write-Success "Files extracted"
    
    # Add to PATH
    if ($AddToPath) {
        Write-Status "Adding to PATH..."
        $currentPath = [System.Environment]::GetEnvironmentVariable("Path", "User")
        if ($currentPath -notlike "*$InstallDir*") {
            [System.Environment]::SetEnvironmentVariable(
                "Path", 
                "$currentPath;$InstallDir", 
                "User"
            )
            $env:Path = "$env:Path;$InstallDir"
        }
        Write-Success "Added to PATH"
    }
    
    # Create Start Menu shortcut
    Write-Status "Creating shortcuts..."
    $shell = New-Object -ComObject WScript.Shell
    $shortcutPath = "$env:APPDATA\Microsoft\Windows\Start Menu\Programs\Loco.lnk"
    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = "$InstallDir\Loco.Cli.exe"
    $shortcut.WorkingDirectory = $InstallDir
    $shortcut.Description = "Loco Automation Platform"
    $shortcut.Save()
    Write-Success "Created Start Menu shortcut"
    
    # Verify installation
    Write-Status "Verifying installation..."
    $testOutput = & "$InstallDir\Loco.Cli.exe" version 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Success "Installation verified"
    } else {
        throw "Installation verification failed"
    }
    
    # Final message
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Green
    Write-Host " Loco installed successfully!" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Installation directory: $InstallDir" -ForegroundColor Yellow
    Write-Host "To get started, run: loco --help" -ForegroundColor Yellow
    Write-Host ""
    
    # Launch welcome message
    if (-not $Silent) {
        Start-Process "$InstallDir\Loco.Cli.exe" -ArgumentList "--help" -NoNewWindow -Wait
    }
    
} catch {
    Write-Error "Installation failed: $_"
    exit 1
}
