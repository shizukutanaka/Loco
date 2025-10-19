@echo off
REM ============================================================================
REM Loco Build Script - Build All Projects
REM ============================================================================

echo.
echo ===================================
echo Loco - Build All Projects
echo ===================================
echo.

REM Change to project root
cd /d "%~dp0.."

REM Clean previous builds
echo [1/4] Cleaning previous builds...
dotnet clean -c Release > nul 2>&1
if exist "publish" rmdir /s /q "publish" > nul 2>&1
echo       ✓ Clean completed

REM Restore dependencies
echo.
echo [2/4] Restoring dependencies...
dotnet restore
if %errorlevel% neq 0 (
    echo       ✗ Restore failed
    exit /b 1
)
echo       ✓ Dependencies restored

REM Build in Release mode
echo.
echo [3/4] Building solution...
dotnet build -c Release --no-restore
if %errorlevel% neq 0 (
    echo       ✗ Build failed
    exit /b 1
)
echo       ✓ Build successful

REM Run tests
echo.
echo [4/4] Running tests...
dotnet test -c Release --no-build --verbosity quiet
if %errorlevel% neq 0 (
    echo       ✗ Tests failed
    exit /b 1
)
echo       ✓ All tests passed

echo.
echo ===================================
echo Build completed successfully!
echo ===================================
echo.
echo Output: src\Loco.Cli\bin\Release\net8.0\
echo.

exit /b 0
