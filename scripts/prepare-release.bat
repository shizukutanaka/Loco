@echo off
REM ===================================
REM Loco - Release Preparation Script
REM ===================================
REM
REM Prepares a complete release package with documentation and examples
REM Creates a release-ready distribution folder
REM

setlocal

echo ===================================
echo Loco - Release Preparation
echo ===================================
echo.

REM Check if version argument is provided
if "%1"=="" (
    echo Error: Version number required
    echo Usage: prepare-release.bat [version]
    echo Example: prepare-release.bat 0.1.0
    exit /b 1
)

set VERSION=%1
set RELEASE_DIR=release-v%VERSION%

echo Preparing release version: %VERSION%
echo.

REM [1/7] Clean previous release
echo [1/7] Cleaning previous release...
if exist %RELEASE_DIR% rmdir /s /q %RELEASE_DIR%
mkdir %RELEASE_DIR%
echo       √ Release directory created

REM [2/7] Build solution
echo [2/7] Building solution...
call scripts\build-all.bat > nul 2>&1
if errorlevel 1 (
    echo       X Build failed
    exit /b 1
)
echo       √ Build successful

REM [3/7] Run tests
echo [3/7] Running tests...
dotnet test -c Release --no-build --verbosity quiet > nul 2>&1
if errorlevel 1 (
    echo       X Tests failed
    exit /b 1
)
echo       √ All tests passed

REM [4/7] Create self-contained executable
echo [4/7] Creating self-contained executable...
dotnet publish src/Loco.Cli/Loco.Cli.csproj -c Release -o %RELEASE_DIR%\bin --self-contained true -r win-x64 /p:PublishSingleFile=true --verbosity quiet
if errorlevel 1 (
    echo       X Publish failed
    exit /b 1
)
echo       √ Executable created

REM [5/7] Copy documentation
echo [5/7] Copying documentation...
mkdir %RELEASE_DIR%\docs
copy README.md %RELEASE_DIR%\ > nul 2>&1
copy QUICKSTART.md %RELEASE_DIR%\ > nul 2>&1
copy LICENSE %RELEASE_DIR%\ > nul 2>&1 || echo # No LICENSE file found
echo       √ Documentation copied

REM [6/7] Copy examples
echo [6/7] Copying examples...
mkdir %RELEASE_DIR%\examples
xcopy examples %RELEASE_DIR%\examples /E /I /Q > nul 2>&1
echo       √ Examples copied

REM [7/7] Create version info
echo [7/7] Creating version info...
(
    echo Loco Automation Platform
    echo Version: %VERSION%
    echo Build Date: %DATE% %TIME%
    echo.
    echo Quick Start:
    echo 1. cd bin
    echo 2. Loco.Cli.exe setup
    echo 3. Loco.Cli.exe health
    echo 4. Loco.Cli.exe help
    echo.
    echo For detailed documentation, see README.md
) > %RELEASE_DIR%\VERSION.txt
echo       √ Version info created

echo.
echo ===================================
echo Release Package Ready!
echo ===================================
echo.
echo Location: %RELEASE_DIR%\
echo Version: %VERSION%
echo.
echo Contents:
echo   - bin\Loco.Cli.exe (self-contained)
echo   - examples\ (workflow and IaC examples)
echo   - docs\ (documentation)
echo   - README.md, QUICKSTART.md
echo   - VERSION.txt
echo.
echo Next steps:
echo   1. Test the release: cd %RELEASE_DIR%\bin ^&^& Loco.Cli.exe version
echo   2. Create archive: tar -czf loco-v%VERSION%-win-x64.tar.gz %RELEASE_DIR%
echo   3. Publish to GitHub Releases
echo.

exit /b 0
