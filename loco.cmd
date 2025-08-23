@echo off
REM Loco launcher - simplified entry point
REM Following principles: simplicity and efficiency

set LOCO_PATH=%~dp0

REM Check if compiled version exists
if exist "%LOCO_PATH%output\Loco.Cli.exe" (
    "%LOCO_PATH%output\Loco.Cli.exe" %*
) else if exist "%LOCO_PATH%output\win-x64\Loco.Cli.exe" (
    "%LOCO_PATH%output\win-x64\Loco.Cli.exe" %*
) else if exist "%LOCO_PATH%src\Loco.Cli\bin\Release\net8.0\Loco.Cli.exe" (
    "%LOCO_PATH%src\Loco.Cli\bin\Release\net8.0\Loco.Cli.exe" %*
) else (
    echo Loco not built. Run: build.bat publish
    exit /b 1
)
