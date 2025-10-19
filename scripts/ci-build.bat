@echo off
REM ===================================
REM Loco - CI/CD Build Script
REM ===================================
REM
REM This script is designed for CI/CD pipelines (GitHub Actions, Azure DevOps, etc.)
REM Performs complete build, test, and package operations with exit codes
REM

setlocal enabledelayedexpansion

echo ===================================
echo Loco - CI/CD Build
echo ===================================
echo.

REM Set error level tracking
set FAILED=0

REM [1/5] Clean
echo [1/5] Cleaning previous builds...
dotnet clean -c Release --verbosity quiet > nul 2>&1
if errorlevel 1 (
    echo       X Clean failed
    set FAILED=1
    goto :end
)
echo       √ Clean completed

REM [2/5] Restore
echo [2/5] Restoring dependencies...
dotnet restore --verbosity quiet
if errorlevel 1 (
    echo       X Restore failed
    set FAILED=1
    goto :end
)
echo       √ Dependencies restored

REM [3/5] Build
echo [3/5] Building solution...
dotnet build -c Release --no-restore --verbosity quiet
if errorlevel 1 (
    echo       X Build failed
    set FAILED=1
    goto :end
)
echo       √ Build successful

REM [4/5] Test
echo [4/5] Running tests...
dotnet test -c Release --no-build --verbosity quiet --logger "console;verbosity=minimal"
if errorlevel 1 (
    echo       X Tests failed
    set FAILED=1
    goto :end
)
echo       √ All tests passed

REM [5/5] Package (if requested)
if "%1"=="--package" (
    echo [5/5] Creating package...
    dotnet publish src/Loco.Cli/Loco.Cli.csproj -c Release -o publish --self-contained true -r win-x64 /p:PublishSingleFile=true --verbosity quiet
    if errorlevel 1 (
        echo       X Packaging failed
        set FAILED=1
        goto :end
    )
    echo       √ Package created in publish/
) else (
    echo [5/5] Skipping package (use --package to create)
)

:end
echo.
echo ===================================
if %FAILED%==0 (
    echo CI Build: SUCCESS
    echo ===================================
    exit /b 0
) else (
    echo CI Build: FAILED
    echo ===================================
    exit /b 1
)
