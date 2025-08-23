@echo off
:: Loco Quick Installer
:: One-click installation for Windows users
:: Following Rob Pike's principle: simple is better

title Loco Quick Installer
cls
color 0A

echo =========================================
echo     Loco - AI Automation Platform
echo         Quick Installer
echo =========================================
echo.

:: Check Windows version
for /f "tokens=4-5 delims=. " %%i in ('ver') do set VERSION=%%i.%%j
if "%VERSION%" LSS "10.0" (
    echo Error: Loco requires Windows 10 or later.
    pause
    exit /b 1
)

:: Check if running as admin
net session >nul 2>&1
if %errorLevel% == 0 (
    set ADMIN=1
    echo [OK] Running with administrator privileges
) else (
    set ADMIN=0
    echo [INFO] Running without administrator privileges
)

echo.
echo Detecting best installation method...
echo.

:: Check for WinGet
where winget >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    echo [FOUND] Windows Package Manager (WinGet)
    set INSTALL_METHOD=winget
    goto :install
)

:: Check for Chocolatey
where choco >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    echo [FOUND] Chocolatey Package Manager
    set INSTALL_METHOD=choco
    goto :install
)

:: Default to portable
echo [INFO] No package manager found. Using portable installation.
set INSTALL_METHOD=portable

:install
echo.
echo Installation method: %INSTALL_METHOD%
echo.

if "%INSTALL_METHOD%"=="winget" (
    echo Installing Loco via WinGet...
    winget install ShizukuTanaka.Loco --silent --accept-package-agreements --accept-source-agreements
    if %ERRORLEVEL% EQU 0 (
        echo.
        echo [SUCCESS] Loco installed successfully via WinGet!
        goto :success
    ) else (
        echo [ERROR] WinGet installation failed. Trying portable installation...
        set INSTALL_METHOD=portable
    )
)

if "%INSTALL_METHOD%"=="choco" (
    if "%ADMIN%"=="0" (
        echo [WARNING] Chocolatey requires administrator privileges.
        echo Please run this installer as Administrator.
        echo.
        echo Falling back to portable installation...
        set INSTALL_METHOD=portable
    ) else (
        echo Installing Loco via Chocolatey...
        choco install loco -y
        if %ERRORLEVEL% EQU 0 (
            echo.
            echo [SUCCESS] Loco installed successfully via Chocolatey!
            goto :success
        ) else (
            echo [ERROR] Chocolatey installation failed. Trying portable installation...
            set INSTALL_METHOD=portable
        )
    )
)

if "%INSTALL_METHOD%"=="portable" (
    echo Installing Loco Portable Version...
    echo.
    
    :: Create installation directory
    set INSTALL_DIR=%LOCALAPPDATA%\Programs\Loco
    if not exist "%INSTALL_DIR%" mkdir "%INSTALL_DIR%"
    
    :: Download using PowerShell
    echo Downloading Loco...
    powershell -NoProfile -ExecutionPolicy Bypass -Command ^
        "$ProgressPreference = 'SilentlyContinue'; ^
        try { ^
            $release = Invoke-RestMethod -Uri 'https://api.github.com/repos/shizukutanaka/Loco/releases/latest' -Headers @{'User-Agent'='Loco-Installer'}; ^
            $asset = $release.assets | Where-Object { $_.name -like '*Portable-x64.zip' } | Select-Object -First 1; ^
            if ($asset) { ^
                $url = $asset.browser_download_url; ^
                $tempFile = Join-Path $env:TEMP 'loco-portable.zip'; ^
                Write-Host 'Downloading from:' $url; ^
                Invoke-WebRequest -Uri $url -OutFile $tempFile -UseBasicParsing; ^
                Write-Host 'Extracting...'; ^
                Expand-Archive -Path $tempFile -DestinationPath '%INSTALL_DIR%' -Force; ^
                Remove-Item $tempFile -Force; ^
                exit 0; ^
            } else { ^
                Write-Host 'ERROR: Could not find portable package'; ^
                exit 1; ^
            } ^
        } catch { ^
            Write-Host 'ERROR:' $_.Exception.Message; ^
            exit 1; ^
        }"
    
    if %ERRORLEVEL% NEQ 0 (
        echo.
        echo [ERROR] Failed to download Loco.
        echo Please check your internet connection and try again.
        pause
        exit /b 1
    )
    
    :: Add to PATH
    echo Adding to PATH...
    setx PATH "%PATH%;%INSTALL_DIR%;%INSTALL_DIR%\bin" >nul 2>&1
    
    :: Create desktop shortcut
    echo Creating desktop shortcut...
    powershell -NoProfile -ExecutionPolicy Bypass -Command ^
        "$WshShell = New-Object -ComObject WScript.Shell; ^
        $Shortcut = $WshShell.CreateShortcut('%USERPROFILE%\Desktop\Loco.lnk'); ^
        $Shortcut.TargetPath = '%INSTALL_DIR%\Loco.exe'; ^
        $Shortcut.WorkingDirectory = '%INSTALL_DIR%'; ^
        $Shortcut.IconLocation = '%INSTALL_DIR%\Loco.exe'; ^
        $Shortcut.Description = 'Loco - AI Automation Platform'; ^
        $Shortcut.Save()"
    
    echo.
    echo [SUCCESS] Loco Portable installed successfully!
)

:success
echo.
echo =========================================
echo     Installation Complete!
echo =========================================
echo.
echo Loco has been installed on your system.
echo.
echo Quick Start:
echo   - Desktop: Double-click the Loco icon
echo   - Terminal: Type 'loco' and press Enter
echo.
echo Example Commands:
echo   loco quick timer 7:00 notify "Good morning!"
echo   loco build
echo   loco gui
echo.
echo Documentation: https://github.com/shizukutanaka/Loco
echo.

:: Check .NET Runtime
echo Checking .NET Runtime...
dotnet --list-runtimes 2>nul | findstr "Microsoft.NETCore.App 8." >nul
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [WARNING] .NET 8.0 Runtime not found.
    echo.
    choice /C YN /M "Would you like to install .NET 8.0 Runtime now"
    if %ERRORLEVEL% EQU 1 (
        echo Installing .NET Runtime...
        start https://dotnet.microsoft.com/download/dotnet/8.0
    )
)

echo.
echo Press any key to exit...
pause >nul
