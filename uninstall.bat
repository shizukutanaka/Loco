@echo off
setlocal enabledelayedexpansion

:: Loco Uninstaller for Windows

echo =========================================
echo    Loco Uninstall Wizard
echo =========================================
echo.

:: Check for administrator privileges
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo This uninstaller requires administrator privileges.
    echo Please run as administrator.
    echo.
    pause
    exit /b 1
)

:: Default installation paths
set DEFAULT_PATH=C:\Program Files\Loco
set CONFIG_DIR=%APPDATA%\Loco

:: Check if Loco is installed
if not exist "%DEFAULT_PATH%" (
    echo Loco installation not found at: %DEFAULT_PATH%
    set /p CUSTOM_PATH="Enter installation path (or press Enter to exit): "
    if "!CUSTOM_PATH!"=="" (
        echo Uninstallation cancelled.
        pause
        exit /b 0
    )
    set DEFAULT_PATH=!CUSTOM_PATH!
)

echo.
echo This will uninstall Loco from:
echo   Program: %DEFAULT_PATH%
echo   Config:  %CONFIG_DIR%
echo.
echo WARNING: This action cannot be undone!
echo.
set /p CONFIRM="Are you sure you want to uninstall Loco? (Y/N): "
if /i "%CONFIRM%" neq "Y" (
    echo Uninstallation cancelled.
    pause
    exit /b 0
)

:: Ask about keeping configuration
echo.
set /p KEEP_CONFIG="Keep configuration and flow files? (Y/N): "

:: Stop any running Loco processes
echo.
echo Stopping Loco processes...
taskkill /f /im "Loco.Cli.exe" >nul 2>&1
taskkill /f /im "loco.exe" >nul 2>&1

:: Remove installation directory
echo Removing program files...
if exist "%DEFAULT_PATH%" (
    rmdir /s /q "%DEFAULT_PATH%"
    if exist "%DEFAULT_PATH%" (
        echo Failed to remove program files. Please close all Loco windows and try again.
        pause
        exit /b 1
    )
)

:: Remove configuration (if requested)
if /i "%KEEP_CONFIG%" neq "Y" (
    echo Removing configuration files...
    if exist "%CONFIG_DIR%" (
        rmdir /s /q "%CONFIG_DIR%"
    )
) else (
    echo Configuration files preserved at: %CONFIG_DIR%
)

:: Remove Start Menu shortcut
echo Removing shortcuts...
set SHORTCUT_PATH=%APPDATA%\Microsoft\Windows\Start Menu\Programs\Loco.lnk
if exist "%SHORTCUT_PATH%" del "%SHORTCUT_PATH%" >nul 2>&1

:: Remove Desktop shortcut
set DESKTOP_SHORTCUT=%USERPROFILE%\Desktop\Loco.lnk
if exist "%DESKTOP_SHORTCUT%" del "%DESKTOP_SHORTCUT%" >nul 2>&1

:: Remove from PATH
echo Removing from system PATH...
:: This is complex in batch, so we'll just notify the user
echo.
echo NOTE: Please manually remove %DEFAULT_PATH% from your system PATH if it was added.

:: Final message
echo.
echo =========================================
echo    Uninstallation Complete
echo =========================================
echo.
echo Loco has been uninstalled from your system.
if /i "%KEEP_CONFIG%"=="Y" (
    echo.
    echo Your configuration and flows have been preserved at:
    echo %CONFIG_DIR%
    echo.
    echo To completely remove all traces, delete this folder manually.
)
echo.
echo Thank you for using Loco!
echo.
pause