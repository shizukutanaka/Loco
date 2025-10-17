# Loco Windows Installer (one-liner friendly)
# Builds from the official repository, validates prerequisites, and installs a single-file CLI

[CmdletBinding()]
param(
    [string]$Version = "latest",
    [string]$InstallDir = "$env:LOCALAPPDATA\Loco",
    [string]$BinDir = "$env:LOCALAPPDATA\Loco\bin",
    [switch]$FrameworkDependent
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$ProgressPreference = 'SilentlyContinue'

$repoOwner = 'shizukutanaka'
$repoName = 'Loco'
$downloadHeaders = @{ 'User-Agent' = 'Loco-Installer' }

function Write-Step {
    param([string]$Message)
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Write-Info {
    param([string]$Message)
    Write-Host "[INFO] $Message" -ForegroundColor DarkCyan
}

function Write-Warn {
    param([string]$Message)
    Write-Host "[WARN] $Message" -ForegroundColor Yellow
}

function Write-ErrorLine {
    param([string]$Message)
    Write-Host "[ERROR] $Message" -ForegroundColor Red
}

function Ensure-Windows10 {
    $version = [System.Environment]::OSVersion.Version
    if ($version.Major -lt 10) {
        throw "Windows 10 or later is required"
    }
}

function Get-Runtime {
    $arch = [System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture
    switch ($arch) {
        'X64' { return 'win-x64' }
        'Arm64' { return 'win-arm64' }
        default { throw "Unsupported architecture: $arch" }
    }
}

function Ensure-DotNet {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw "dotnet SDK 8.0 or later is required. Install from https://dotnet.microsoft.com/download/dotnet/8.0 and re-run."
    }
    $sdks = & dotnet --list-sdks
    if ($sdks -notmatch '^8\.') {
        Write-Warn "dotnet 8 SDK not detected. Attempting build with available SDKs."
    }
}

function Invoke-Download {
    param(
        [Parameter(Mandatory = $true)][string]$Uri,
        [Parameter(Mandatory = $true)][string]$Destination
    )
    Invoke-WebRequest -Uri $Uri -Headers $downloadHeaders -OutFile $Destination
}

function Invoke-ProcessSafe {
    param(
        [string]$FilePath,
        [string[]]$ArgumentList,
        [string]$WorkingDirectory
    )

    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $FilePath
    $psi.WorkingDirectory = $WorkingDirectory
    $psi.RedirectStandardError = $true
    $psi.RedirectStandardOutput = $true
    $psi.UseShellExecute = $false
    foreach ($arg in $ArgumentList) { [void]$psi.ArgumentList.Add($arg) }

    $process = [System.Diagnostics.Process]::Start($psi)
    $process.WaitForExit()

    if ($process.ExitCode -ne 0) {
        $stdout = $process.StandardOutput.ReadToEnd()
        $stderr = $process.StandardError.ReadToEnd()
        throw "Command '$FilePath $($ArgumentList -join ' ')'
failed with exit code $($process.ExitCode).
STDOUT:
$stdout
STDERR:
$stderr"
    }
}

function Ensure-Directory {
    param([string]$Path)
    if (-not (Test-Path $Path)) {
        [void](New-Item -ItemType Directory -Path $Path -Force)
    }
}

function Get-SourceArchiveUri {
    param([string]$RequestedVersion)
    if ($RequestedVersion -eq 'latest') {
        return "https://codeload.github.com/$repoOwner/$repoName/zip/refs/heads/main"
    }
    return "https://codeload.github.com/$repoOwner/$repoName/zip/refs/tags/$RequestedVersion"
}

function Add-ToPathIfNeeded {
    param([string]$PathToAdd)

    $currentUserPath = [System.Environment]::GetEnvironmentVariable('Path', 'User')
    if ([string]::IsNullOrEmpty($currentUserPath)) {
        $currentUserPath = ''
    }
    if ($currentUserPath.Split(';', [System.StringSplitOptions]::RemoveEmptyEntries) -notcontains $PathToAdd) {
        [System.Environment]::SetEnvironmentVariable('Path', ($currentUserPath.TrimEnd(';') + ';' + $PathToAdd).Trim(';'), 'User')
        Write-Info "Added $PathToAdd to the user PATH"
    } else {
        Write-Info "$PathToAdd already present in user PATH"
    }
}

try {
    Write-Step "Preparing Loco installation"
    Ensure-Windows10
    Ensure-DotNet

    $runtime = Get-Runtime
    Write-Info "Target runtime: $runtime"

    $tempRoot = New-Item -ItemType Directory -Path ([IO.Path]::Combine([IO.Path]::GetTempPath(), "loco-install-" + [Guid]::NewGuid()))
    $null = $tempRoot
    $tempRootPath = $tempRoot.FullName
    $archivePath = Join-Path $tempRootPath 'loco-source.zip'
    $sourceDir = Join-Path $tempRootPath 'src'
    Ensure-Directory $sourceDir

    Write-Step "Downloading repository source"
    $sourceUri = Get-SourceArchiveUri -RequestedVersion $Version
    Invoke-Download -Uri $sourceUri -Destination $archivePath
    Expand-Archive -Path $archivePath -DestinationPath $sourceDir -Force

    $extractedRoot = Get-ChildItem -Path $sourceDir -Directory | Select-Object -First 1
    if (-not $extractedRoot) {
        throw "Failed to extract source archive"
    }
    $repoRoot = $extractedRoot.FullName

    Write-Step "Publishing Loco CLI"
    $publishDir = Join-Path $tempRootPath 'publish'
    Ensure-Directory $publishDir

    $publishArgs = @('publish', (Join-Path $repoRoot 'src/Loco.Cli'), '-c', 'Release', '-r', $runtime, '-p:PublishSingleFile=true', '-p:IncludeNativeLibrariesForSelfExtract=true', '-o', $publishDir)
    if ($FrameworkDependent.IsPresent) {
        $publishArgs += @('--self-contained', 'false')
    } else {
        $publishArgs += @('--self-contained', 'true')
    }
    Invoke-ProcessSafe -FilePath 'dotnet' -ArgumentList $publishArgs -WorkingDirectory $repoRoot

    $cliBinary = Join-Path $publishDir 'Loco.Cli.exe'
    if (-not (Test-Path $cliBinary)) {
        throw "Loco.Cli.exe not found after publish"
    }

    Write-Step "Installing files"
    Ensure-Directory $InstallDir
    $installBinDir = Join-Path $InstallDir 'bin'
    Ensure-Directory $installBinDir
    Ensure-Directory $BinDir

    Copy-Item -Path $cliBinary -Destination (Join-Path $installBinDir 'Loco.Cli.exe') -Force

    $shimPath = Join-Path $BinDir 'loco.cmd'
    Set-Content -LiteralPath $shimPath -Encoding ASCII -Value "@echo off`r`n""%~dp0..\Loco\bin\Loco.Cli.exe"" %*"

    $pwShimPath = Join-Path $BinDir 'loco.ps1'
    Set-Content -LiteralPath $pwShimPath -Encoding UTF8 -Value "& \"$installBinDir\Loco.Cli.exe\" @Args"

    Add-ToPathIfNeeded -PathToAdd $BinDir

    Write-Step "Recording installation metadata"
    $configDir = Join-Path $InstallDir 'config'
    Ensure-Directory $configDir
    $metadata = [ordered]@{
        version = $Version
        runtime = $runtime
        installDir = $InstallDir
        binDir = $BinDir
        frameworkDependent = $FrameworkDependent.IsPresent
        installedAt = [DateTime]::UtcNow.ToString('o')
    }
    $metadata | ConvertTo-Json -Depth 4 | Set-Content -Path (Join-Path $configDir 'install.json') -Encoding UTF8

    Write-Step "Verifying CLI"
    $cliResult = & "$installBinDir/Loco.Cli.exe" --version
    if ($LASTEXITCODE -ne 0) {
        throw "Verification failed: $cliResult"
    }
    Write-Info $cliResult

    Write-Host "";
    Write-Host "========================================" -ForegroundColor Green
    Write-Host " Loco installed successfully" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "Installation directory : $InstallDir" -ForegroundColor DarkGray
    Write-Host "User binary directory : $BinDir" -ForegroundColor DarkGray
    Write-Host "Run 'loco --help' in a new shell session." -ForegroundColor DarkGray

} catch {
    Write-ErrorLine "Installation failed: $_"
    exit 1
} finally {
    if ($tempRootPath -and (Test-Path $tempRootPath)) {
        Remove-Item -Path $tempRootPath -Recurse -Force -ErrorAction SilentlyContinue
    }
}
