@echo off
setlocal enabledelayedexpansion

echo ====================================
echo     Loco MSI Installer Builder
echo ====================================
echo.

REM Check for WiX Toolset
wix --version >nul 2>&1
if %errorlevel% neq 0 (
    echo ERROR: WiX Toolset 4+ required
    echo Download from: https://wixtoolset.org/
    exit /b 1
)

REM Check for .NET SDK
dotnet --version >nul 2>&1
if %errorlevel% neq 0 (
    echo ERROR: .NET SDK 8.0+ required
    exit /b 1
)

REM Parse arguments
set TARGET=%1
if "%TARGET%"=="" set TARGET=build

if "%TARGET%"=="clean" (
    echo [CLEAN] Removing installer artifacts...
    if exist installer\obj rmdir /s /q installer\obj
    if exist installer\bin rmdir /s /q installer\bin
    del /q installer\*.msi 2>nul
    del /q installer\*.wixpdb 2>nul
    echo Clean complete.
    exit /b 0
)

if "%TARGET%"=="build" (
    echo [BUILD] Building Loco MSI Installer...

    REM Ensure output directory exists
    if not exist output (
        echo ERROR: Run build.bat publish first to create output files
        exit /b 1
    )

    REM Create installer directories
    if not exist installer\bin mkdir installer\bin

    REM Compile WiX source
    echo Compiling WiX source...
    wix build installer\LocoInstaller.wxs -o installer\bin\LocoInstaller.msi

    if %errorlevel% neq 0 (
        echo Build failed!
        exit /b 1
    )

    REM Get file size
    for %%I in (installer\bin\LocoInstaller.msi) do set SIZE=%%~zI
    set /a SIZE_MB=%SIZE% / 1048576

    echo.
    echo ====================================
    echo     Build Complete!
    echo ====================================
    echo.
    echo Platform: Windows x64
    echo Installer Size: %SIZE_MB% MB
    echo Output: installer\bin\LocoInstaller.msi
    echo.
    echo To install: msiexec /i installer\bin\LocoInstaller.msi
    echo To uninstall: msiexec /x installer\bin\LocoInstaller.msi
    exit /b 0
)

if "%TARGET%"=="test" (
    echo [TEST] Testing Loco MSI Installer...

    REM Check if installer exists
    if not exist installer\bin\LocoInstaller.msi (
        echo ERROR: Run installer.bat build first
        exit /b 1
    )

    REM Create test directory
    if not exist test-install mkdir test-install

    REM Test installation (silent install to test directory)
    echo Testing installation...
    msiexec /i installer\bin\LocoInstaller.msi /quiet /norestart INSTALLDIR="test-install"

    if %errorlevel% neq 0 (
        echo Installation test failed!
        exit /b 1
    )

    REM Verify installation
    if exist test-install\Loco.Cli.exe (
        echo ✓ Installation successful
    ) else (
        echo ✗ Installation failed - executable not found
        exit /b 1
    )

    REM Test basic functionality
    echo Testing basic functionality...
    test-install\Loco.Cli.exe --version >nul 2>&1
    if %errorlevel% neq 0 (
        echo ✗ Version command failed
        exit /b 1
    )

    REM Test uninstallation
    echo Testing uninstallation...
    msiexec /x installer\bin\LocoInstaller.msi /quiet /norestart

    if %errorlevel% neq 0 (
        echo Uninstallation test failed!
        exit /b 1
    )

    REM Cleanup
    if exist test-install rmdir /s /q test-install

    echo.
    echo ====================================
    echo     Test Complete!
    echo ====================================
    echo.
    echo All tests passed! ✓
    exit /b 0
)

echo Unknown target: %TARGET%
echo.
echo Usage: installer.bat [target]
echo.
echo Main Targets:
echo   build       - Build the MSI installer (requires published output)
echo   test        - Test the MSI installer
echo   clean       - Clean installer artifacts
echo.
exit /b 1
