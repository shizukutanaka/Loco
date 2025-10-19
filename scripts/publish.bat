@echo off
REM ============================================================================
REM Loco Publish Script - Create Production Build
REM ============================================================================

echo.
echo ===================================
echo Loco - Create Production Build
echo ===================================
echo.

REM Change to project root
cd /d "%~dp0.."

REM Set output directory
set OUTPUT_DIR=publish
set VERSION=0.1.0-alpha

echo Configuration:
echo   Version: %VERSION%
echo   Output:  %OUTPUT_DIR%
echo.

REM Clean output directory
if exist "%OUTPUT_DIR%" (
    echo [1/5] Cleaning output directory...
    rmdir /s /q "%OUTPUT_DIR%" > nul 2>&1
    echo       ✓ Cleaned
)

REM Create output directory
mkdir "%OUTPUT_DIR%" > nul 2>&1

REM Build and publish
echo.
echo [2/5] Publishing Loco.Cli (self-contained)...
dotnet publish src\Loco.Cli\Loco.Cli.csproj ^
    -c Release ^
    -r win-x64 ^
    --self-contained true ^
    -p:PublishSingleFile=true ^
    -p:IncludeNativeLibrariesForSelfExtract=true ^
    -p:Version=%VERSION% ^
    -o "%OUTPUT_DIR%" ^
    --verbosity quiet

if %errorlevel% neq 0 (
    echo       ✗ Publish failed
    exit /b 1
)
echo       ✓ Published successfully

REM Copy examples and documentation
echo.
echo [3/5] Copying examples...
xcopy /E /I /Q examples "%OUTPUT_DIR%\examples" > nul 2>&1
echo       ✓ Examples copied

echo.
echo [4/5] Copying documentation...
copy README.md "%OUTPUT_DIR%\" > nul 2>&1
copy QUICKSTART.md "%OUTPUT_DIR%\" > nul 2>&1 2>nul
copy LICENSE "%OUTPUT_DIR%\" > nul 2>&1 2>nul
echo       ✓ Documentation copied

REM Create version file
echo.
echo [5/5] Creating version file...
(
    echo Loco CLI v%VERSION%
    echo Build Date: %date% %time%
    echo Platform: Windows x64
    echo Runtime: .NET 8.0
) > "%OUTPUT_DIR%\VERSION.txt"
echo       ✓ Version file created

echo.
echo ===================================
echo Production build completed!
echo ===================================
echo.
echo Output directory: %OUTPUT_DIR%
echo Executable: Loco.Cli.exe
echo.
echo To test the build:
echo   cd %OUTPUT_DIR%
echo   Loco.Cli.exe --help
echo.

exit /b 0
