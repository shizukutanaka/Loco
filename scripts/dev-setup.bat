@echo off
REM ===================================
REM Loco - Development Environment Setup
REM ===================================
REM
REM Sets up a complete development environment for Loco
REM Installs dependencies, configures IDE, and validates setup
REM

setlocal

echo ===================================
echo Loco - Development Setup
echo ===================================
echo.

REM [1/6] Check .NET SDK
echo [1/6] Checking .NET SDK...
dotnet --version > nul 2>&1
if errorlevel 1 (
    echo       X .NET SDK not found
    echo.
    echo Please install .NET 8.0 SDK from:
    echo https://dotnet.microsoft.com/download
    exit /b 1
)
for /f "tokens=*" %%v in ('dotnet --version') do set DOTNET_VERSION=%%v
echo       √ .NET SDK %DOTNET_VERSION% found

REM [2/6] Check Git
echo [2/6] Checking Git...
git --version > nul 2>&1
if errorlevel 1 (
    echo       X Git not found
    echo.
    echo Please install Git from:
    echo https://git-scm.com/downloads
    exit /b 1
)
echo       √ Git found

REM [3/6] Restore dependencies
echo [3/6] Restoring NuGet packages...
dotnet restore --verbosity quiet
if errorlevel 1 (
    echo       X Restore failed
    exit /b 1
)
echo       √ Dependencies restored

REM [4/6] Build solution
echo [4/6] Building solution...
dotnet build -c Debug --verbosity quiet
if errorlevel 1 (
    echo       X Build failed
    exit /b 1
)
echo       √ Build successful

REM [5/6] Run tests
echo [5/6] Running tests...
dotnet test -c Debug --no-build --verbosity quiet
if errorlevel 1 (
    echo       X Tests failed
    exit /b 1
)
echo       √ All tests passed

REM [6/6] Create workspace directories
echo [6/6] Creating workspace directories...
if not exist "workflows" mkdir workflows
if not exist "rules" mkdir rules
if not exist "logs" mkdir logs
if not exist "backups" mkdir backups
echo       √ Workspace directories created

echo.
echo ===================================
echo Development Environment Ready!
echo ===================================
echo.
echo Next steps:
echo   1. Open solution in Visual Studio or VS Code
echo   2. Run quick test: scripts\quick-test.bat
echo   3. Start coding!
echo.
echo Useful commands:
echo   scripts\quick-test.bat     - Fast verification
echo   scripts\build-all.bat      - Full build and test
echo   scripts\run-examples.bat   - Validate examples
echo.
echo Project structure:
echo   src\Loco.Cli\     - CLI application
echo   src\Loco.Core\    - Core automation engine
echo   tests\            - Unit and integration tests
echo   examples\         - Example workflows and IaC
echo   scripts\          - Build and automation scripts
echo.

exit /b 0
