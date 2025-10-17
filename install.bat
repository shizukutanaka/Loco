@echo off
setlocal

set SCRIPT_DIR=%~dp0
set PS_SCRIPT=%SCRIPT_DIR%install-oneliner.ps1

if not exist "%PS_SCRIPT%" (
    echo ERROR: Required script install-oneliner.ps1 is missing.
    exit /b 1
)

where powershell >nul 2>&1
if errorlevel 1 (
    echo ERROR: PowerShell is required to run this installer.
    exit /b 1
)

powershell -NoProfile -ExecutionPolicy Bypass -File "%PS_SCRIPT%" %*
set EXITCODE=%ERRORLEVEL%

if %EXITCODE% NEQ 0 (
    echo Installation failed with exit code %EXITCODE%.
    exit /b %EXITCODE%
)

echo Loco installation finished successfully.
echo Launch a new terminal and run "loco --help" to verify the CLI.

endlocal
exit /b 0
