@echo off
setlocal enabledelayedexpansion

echo ====================================
echo     Loco Build System
echo     Production-Ready Automation
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

REM Security check
if "%TARGET%"=="security" (
    echo [SECURITY] Running security scan...
    dotnet list package --vulnerable --include-transitive
    if %errorlevel% neq 0 (
        echo Security vulnerabilities detected!
        exit /b 1
    )
    echo No security vulnerabilities found.
    exit /b 0
)

REM Code analysis
if "%TARGET%"=="analyze" (
    echo [ANALYZE] Running code analysis...
    dotnet build -c Release /p:RunAnalyzersDuringBuild=true /p:TreatWarningsAsErrors=true
    if %errorlevel% neq 0 (
        echo Code analysis failed!
        exit /b 1
    )
    echo Code analysis passed.
    exit /b 0
)

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
    dotnet build -c Release --nologo --verbosity minimal
    if %errorlevel% neq 0 (
        echo Build failed!
        exit /b 1
    )
    echo Build complete.
    exit /b 0
)

if "%TARGET%"=="test" (
    echo [TEST] Running tests...
    dotnet test -c Release --nologo --logger:"console;verbosity=normal" --no-build
    exit /b %errorlevel%
)

if "%TARGET%"=="benchmark" (
    echo [BENCHMARK] Running benchmarks...
    dotnet run -c Release --project tests\Loco.Tests\Loco.Tests.csproj --filter "*Benchmark*"
    exit /b %errorlevel%
)

if "%TARGET%"=="publish" (
    echo [PUBLISH] Creating release package...
    
    REM Create output directory
    if not exist output mkdir output
    
    REM Windows x64
    echo Publishing for Windows x64...
    dotnet publish src\Loco.Cli\Loco.Cli.csproj ^
        -c Release ^
        -r win-x64 ^
        --self-contained true ^
        -p:PublishSingleFile=true ^
        -p:PublishTrimmed=true ^
        -p:TrimMode=partial ^
        -p:EnableCompressionInSingleFile=true ^
        -o output\win-x64 ^
        --nologo --verbosity minimal
    
    if %errorlevel% neq 0 (
        echo Publish failed!
        exit /b 1
    )
    
    REM Create launcher
    echo @echo off > output\loco.bat
    echo "%%~dp0win-x64\Loco.Cli.exe" %%* >> output\loco.bat
    
    REM Copy resources
    xcopy examples output\examples\ /E /I /Q /Y >nul 2>&1
    copy README.md output\ /Y >nul 2>&1
    copy LICENSE output\ /Y >nul 2>&1
    
    REM Get file size
    for %%I in (output\win-x64\Loco.Cli.exe) do set SIZE=%%~zI
    set /a SIZE_MB=%SIZE% / 1048576
    
    echo.
    echo ====================================
    echo     Build Complete!
    echo ====================================
    echo.
    echo Platform: Windows x64
    echo Executable Size: %SIZE_MB% MB
    echo Output: output\
    echo.
    echo Run: output\loco.bat --help
    exit /b 0
)

if "%TARGET%"=="publish-all" (
    echo [PUBLISH-ALL] Creating multi-platform releases...
    
    REM Windows
    call :publish-platform win-x64 Windows-x64
    call :publish-platform win-arm64 Windows-ARM64
    
    REM Linux
    call :publish-platform linux-x64 Linux-x64
    call :publish-platform linux-arm64 Linux-ARM64
    call :publish-platform linux-arm Linux-ARM
    
    REM macOS
    call :publish-platform osx-x64 macOS-x64
    call :publish-platform osx-arm64 macOS-ARM64
    
    echo.
    echo All platforms published successfully!
    exit /b 0
)

if "%TARGET%"=="docker" (
    echo [DOCKER] Building Docker image...
    docker build -t loco:latest .
    if %errorlevel% neq 0 (
        echo Docker build failed!
        exit /b 1
    )
    docker tag loco:latest loco:1.1.0
    echo Docker image built: loco:latest
    exit /b 0
)

if "%TARGET%"=="web" (
    echo [WEB] Building Web UI...
    if not exist "web\package.json" (
        echo Web project not found, skipping.
        exit /b 0
    )
    pushd web
    if not exist node_modules (
        echo Installing dependencies...
        npm ci --silent
    )
    echo Building...
    npm run build --silent
    if %errorlevel% neq 0 (
        popd
        exit /b 1
    )
    popd
    echo Web UI built successfully.
    exit /b 0
)

if "%TARGET%"=="mobile" (
    echo [MOBILE] Preparing Mobile App...
    if not exist "mobile\package.json" (
        echo Mobile project not found, skipping.
        exit /b 0
    )
    pushd mobile
    if not exist node_modules (
        echo Installing dependencies...
        npm ci --silent
    )
    popd
    echo Mobile app prepared.
    exit /b 0
)

if "%TARGET%"=="install" (
    echo [INSTALL] Installing Loco...
    call %0 publish
    if %errorlevel% neq 0 exit /b 1
    
    REM Add to PATH
    echo Adding to PATH...
    setx PATH "%PATH%;%CD%\output" >nul 2>&1
    
    echo Installation complete!
    echo Please restart your terminal to use 'loco' command.
    exit /b 0
)

if "%TARGET%"=="quick" (
    echo [QUICK] Quick build and test...
    call %0 clean
    call %0 build
    call %0 test
    exit /b 0
)

if "%TARGET%"=="release" (
    echo [RELEASE] Creating production release...
    call %0 clean
    call %0 restore
    call %0 build
    call %0 test
    call %0 benchmark
    call %0 publish
    
    REM Create release archive
    echo Creating release archive...
    powershell -Command "Compress-Archive -Path 'output\*' -DestinationPath 'loco-win-x64.zip' -Force"
    
    echo Release created: loco-win-x64.zip
    exit /b 0
)

if "%TARGET%"=="all" (
    call %0 clean
    call %0 restore
    call %0 build
    call %0 test
    call %0 publish
    call %0 web
    call %0 mobile
    call %0 docker
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
echo   install     - Install Loco to system
echo   clean       - Clean build artifacts
echo.
echo Advanced Targets:
echo   restore     - Restore NuGet packages
echo   benchmark   - Run performance benchmarks
echo   publish-all - Create releases for all platforms
echo   docker      - Build Docker image
echo   web         - Build Web UI
echo   mobile      - Prepare Mobile App
echo   quick       - Quick build and test
echo   release     - Create production release with archive
echo   all         - Run all build targets
echo.
exit /b 1

:publish-platform
echo Publishing for %2...
dotnet publish src\Loco.Cli\Loco.Cli.csproj ^
    -c Release ^
    -r %1 ^
    --self-contained true ^
    -p:PublishSingleFile=true ^
    -p:PublishTrimmed=true ^
    -o output\%1 ^
    --nologo --verbosity quiet
if %errorlevel% neq 0 (
    echo Failed to publish %2
    exit /b 1
)
exit /b 0

endlocal
