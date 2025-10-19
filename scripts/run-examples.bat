@echo off
REM ============================================================================
REM Loco Run Examples Script - Test workflow and IaC examples
REM ============================================================================

echo.
echo ===================================
echo Loco - Run Example Validation
echo ===================================
echo.

cd /d "%~dp0.."

set EXE=.\src\Loco.Cli\bin\Release\net8.0\Loco.Cli.exe

REM Check if executable exists
if not exist "%EXE%" (
    echo Error: Loco.Cli.exe not found
    echo Please run: scripts\build-all.bat
    exit /b 1
)

echo Testing workflow examples...
echo.

REM Test workflow visualizations
echo [1/7] Testing system-monitoring.json...
%EXE% workflow examples\workflows\system-monitoring.json --visualize compact > nul 2>&1
if %errorlevel% neq 0 (
    echo       ✗ Failed
    exit /b 1
)
echo       ✓ OK

echo [2/7] Testing daily-backup.json...
%EXE% workflow examples\workflows\daily-backup.json --visualize compact > nul 2>&1
if %errorlevel% neq 0 (
    echo       ✗ Failed
    exit /b 1
)
echo       ✓ OK

echo [3/7] Testing parallel-processing.json...
%EXE% workflow examples\workflows\parallel-processing.json --visualize compact > nul 2>&1
if %errorlevel% neq 0 (
    echo       ✗ Failed
    exit /b 1
)
echo       ✓ OK

echo [4/7] Testing log-cleanup.json...
%EXE% workflow examples\workflows\log-cleanup.json --visualize compact > nul 2>&1
if %errorlevel% neq 0 (
    echo       ✗ Failed
    exit /b 1
)
echo       ✓ OK

echo [5/7] Testing database-backup.json...
%EXE% workflow examples\workflows\database-backup.json --visualize compact > nul 2>&1
if %errorlevel% neq 0 (
    echo       ✗ Failed
    exit /b 1
)
echo       ✓ OK

echo [6/7] Testing dev-environment-setup.json...
%EXE% workflow examples\workflows\dev-environment-setup.json --visualize compact > nul 2>&1
if %errorlevel% neq 0 (
    echo       ✗ Failed
    exit /b 1
)
echo       ✓ OK

echo.
echo Testing IaC examples...
echo.

echo [7/7] Validating all IaC files...

for %%f in (examples\iac\*.yaml) do (
    echo       Validating %%~nxf...
    %EXE% iac validate "%%f" > nul 2>&1
    if !errorlevel! neq 0 (
        echo       ✗ Validation failed: %%~nxf
        %EXE% iac validate "%%f"
        exit /b 1
    )
)
echo       ✓ All IaC files valid

echo.
echo ===================================
echo All examples validated! ✓
echo ===================================
echo.
echo You can run examples with:
echo   %EXE% workflow [file] --visualize
echo   %EXE% iac validate [file]
echo.

exit /b 0
