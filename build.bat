@echo off
setlocal enabledelayedexpansion

echo ====================================
echo     Loco Build System
echo     Lightweight Automation
echo ====================================
echo.

REM Check for .NET SDK
dotnet --version >nul 2>&1
if %errorlevel% neq 0 (
    echo ERROR: .NET SDK 8.0+ required
    echo Download from: https://dot.net
    exit /b 1
)

REM Parse arguments
set TARGET=%1
if "%TARGET%"=="" set TARGET=build

REM Performance measurement
set START_TIME=%TIME%

if "%TARGET%"=="clean" (
    echo [CLEAN] Removing build artifacts...
    powershell -ExecutionPolicy Bypass -Command ^
        "Get-ChildItem -Path . -Include bin,obj,publish,output,TestResults -Recurse -Directory | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue"
    echo Clean complete.
    exit /b 0
)

if "%TARGET%"=="restore" (
    echo [RESTORE] Restoring packages...
    dotnet restore --nologo --verbosity minimal
    exit /b %errorlevel%
)

if "%TARGET%"=="build" (
    echo [BUILD] Building Loco...
    dotnet build -c Release --nologo --verbosity minimal --no-incremental
    if %errorlevel% neq 0 (
        echo Build failed!
        exit /b 1
    )
    echo Build complete.
    exit /b 0
)

if "%TARGET%"=="test" (
    echo [TEST] Running tests...
    dotnet test -c Release --nologo --logger:"console;verbosity=minimal" --no-build --no-restore
    exit /b %errorlevel%
)

if "%TARGET%"=="publish" (
    echo [PUBLISH] Creating production release package...

    set "OUTPUT_DIR=publish-production"
    set "RUNTIME=win-x64"

    REM Clean previous builds
    if exist "%OUTPUT_DIR%" rmdir /s /q "%OUTPUT_DIR%" 2>nul
    if exist "src\Loco.Cli\bin\Release" rmdir /s /q "src\Loco.Cli\bin\Release" 2>nul
    if exist "src\Loco.Core\bin\Release" rmdir /s /q "src\Loco.Core\bin\Release" 2>nul

    REM Restore and build
    echo Restoring packages...
    dotnet restore --nologo --verbosity minimal
    if %errorlevel% neq 0 exit /b 1

    echo Building solution...
    dotnet build -c Release --no-restore --nologo --verbosity minimal
    if %errorlevel% neq 0 exit /b 1

    REM Publish CLI
    echo Publishing for %RUNTIME%...
    dotnet publish src\Loco.Cli\Loco.Cli.csproj ^
        -c Release ^
        -r %RUNTIME% ^
        --self-contained true ^
        -o "%OUTPUT_DIR%" ^
        /p:PublishSingleFile=true ^
        /p:PublishReadyToRun=true ^
        /p:PublishTrimmed=false ^
        /p:IncludeNativeLibrariesForSelfExtract=true ^
        /p:DebugType=None ^
        /p:DebugSymbols=false ^
        --nologo --verbosity minimal

    if %errorlevel% neq 0 (
        echo Publish failed!
        exit /b 1
    )

    REM Create package structure
    mkdir "%OUTPUT_DIR%\config" 2>nul
    mkdir "%OUTPUT_DIR%\workflows" 2>nul

    REM Copy documentation
    copy /y README.md "%OUTPUT_DIR%\" >nul 2>nul
    copy /y QUICK_START.md "%OUTPUT_DIR%\" >nul 2>nul
    copy /y GETTING_STARTED.md "%OUTPUT_DIR%\" >nul 2>nul
    copy /y FAQ.md "%OUTPUT_DIR%\" >nul 2>nul
    copy /y TROUBLESHOOTING.md "%OUTPUT_DIR%\" >nul 2>nul

    REM Copy installation scripts
    copy /y install.bat "%OUTPUT_DIR%\" >nul 2>nul
    copy /y install.sh "%OUTPUT_DIR%\" >nul 2>nul
    copy /y install-oneliner.ps1 "%OUTPUT_DIR%\" >nul 2>nul

    REM Copy example workflows
    if exist "workflows\*.json" copy /y workflows\*.json "%OUTPUT_DIR%\workflows\" >nul 2>nul

    REM Get file size
    for %%I in ("%OUTPUT_DIR%\Loco.Cli.exe") do set SIZE=%%~zI
    set /a SIZE_MB=%SIZE% / 1048576

    echo.
    echo ====================================
    echo     Build Complete!
    echo ====================================
    echo.
    echo Platform: %RUNTIME%
    echo Executable Size: %SIZE_MB% MB
    echo Output: %OUTPUT_DIR%\
    echo.
    echo Verification:
    echo   %OUTPUT_DIR%\Loco.Cli.exe --version
    echo   %OUTPUT_DIR%\Loco.Cli.exe health
    exit /b 0
)

if "%TARGET%"=="quick" (
    echo [QUICK] Quick build and test...
    call %0 clean
    call %0 build
    call %0 test
    exit /b 0
)

echo Unknown target: %TARGET%
echo.
echo Usage: build.bat [target]
echo.
echo Main Targets:
echo   build       - Build the solution (default)
echo   test        - Run unit tests
echo   publish     - Create Windows x64 release
echo   clean       - Clean build artifacts
echo.
echo Advanced Targets:
echo   restore     - Restore NuGet packages
echo   quick       - Quick build and test
echo.
exit /b 1
