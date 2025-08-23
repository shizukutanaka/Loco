$ErrorActionPreference = "Stop"

# Loco Chocolatey Install Script
# Following Rob Pike's simplicity principle

$toolsDir = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$packageName = 'loco'
$softwareName = 'Loco*'
$installerType = 'msi'
$silentArgs = '/qn /norestart'
$validExitCodes = @(0, 3010, 1641)

$packageArgs = @{
  packageName   = $packageName
  unzipLocation = $toolsDir
  fileType      = $installerType
  url           = 'https://github.com/shizukutanaka/Loco/releases/download/v0.0.1/Loco-0.0.1-x86.msi'
  url64bit      = 'https://github.com/shizukutanaka/Loco/releases/download/v0.0.1/Loco-0.0.1-x64.msi'
  softwareName  = $softwareName
  checksum      = 'PLACEHOLDER_CHECKSUM_32'
  checksumType  = 'sha256'
  checksum64    = 'PLACEHOLDER_CHECKSUM_64'
  checksumType64= 'sha256'
  silentArgs    = $silentArgs
  validExitCodes= $validExitCodes
}

# Check .NET 8.0 Runtime
Write-Host "Checking .NET 8.0 Runtime..." -ForegroundColor Cyan
$dotnetVersion = dotnet --list-runtimes 2>$null | Where-Object { $_ -match "Microsoft.NETCore.App 8\." }
if (-not $dotnetVersion) {
    Write-Warning ".NET 8.0 Runtime not found. Installing..."
    choco install dotnet-8.0-runtime -y
}

# Install Loco
Install-ChocolateyPackage @packageArgs

# Add to PATH
$locoPath = "$env:ProgramFiles\Loco"
if (Test-Path $locoPath) {
    Install-ChocolateyPath -PathToInstall $locoPath -PathType 'Machine'
}

# Create desktop shortcut
$desktop = [System.Environment]::GetFolderPath("Desktop")
$shortcutPath = Join-Path $desktop "Loco.lnk"
$targetPath = Join-Path $locoPath "Loco.exe"

if (Test-Path $targetPath) {
    Install-ChocolateyShortcut `
        -ShortcutFilePath $shortcutPath `
        -TargetPath $targetPath `
        -IconLocation $targetPath `
        -Description "Loco - AI Automation Platform"
}

Write-Host "Loco has been installed successfully!" -ForegroundColor Green
Write-Host "You can now use 'loco' command from anywhere." -ForegroundColor Yellow
Write-Host "Desktop shortcut created: $shortcutPath" -ForegroundColor Yellow
