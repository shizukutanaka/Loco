#!/bin/bash

# Loco Build Script for Linux/macOS
# Following Rob Pike's simplicity and portability

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Detect OS
OS="Unknown"
if [[ "$OSTYPE" == "linux-gnu"* ]]; then
    OS="Linux"
elif [[ "$OSTYPE" == "darwin"* ]]; then
    OS="macOS"
elif [[ "$OSTYPE" == "cygwin" ]] || [[ "$OSTYPE" == "msys" ]] || [[ "$OSTYPE" == "win32" ]]; then
    OS="Windows"
fi

echo -e "${GREEN}=====================================${NC}"
echo -e "${GREEN}     Loco Build System ($OS)${NC}"
echo -e "${GREEN}=====================================${NC}"
echo

# Check for .NET SDK
if ! command -v dotnet &> /dev/null; then
    echo -e "${RED}ERROR: .NET SDK not found${NC}"
    echo "Please install from https://dot.net"
    exit 1
fi

# Parse arguments
TARGET=${1:-build}

# --- Target: clean ---
if [[ "$TARGET" == "clean" ]]; then
    echo -e "${YELLOW}[CLEAN] Removing build artifacts...${NC}"
    if command -v pwsh &> /dev/null && [ -f cleanup.ps1 ]; then
        echo -e "${GREEN}[INFO] Advanced cleanup script found, executing...${NC}"
        pwsh -Command "./cleanup.ps1"
    else
        echo -e "${GREEN}[INFO] Basic cleanup...${NC}"
        find . -type d -name "bin" -exec rm -rf {} + 2>/dev/null || true
        find . -type d -name "obj" -exec rm -rf {} + 2>/dev/null || true
        rm -rf output publish TestResults 2>/dev/null || true
    fi
    echo -e "${GREEN}Clean complete.${NC}"
    exit 0
fi

# --- Target: build ---
if [[ "$TARGET" == "build" ]]; then
    echo -e "${YELLOW}[BUILD] Building Loco...${NC}"
    dotnet build -c Release --nologo
    if [ $? -ne 0 ]; then echo -e "${RED}Build failed!${NC}"; exit 1; fi
    echo -e "${GREEN}Build complete.${NC}"
    exit 0
fi

# --- Target: benchmark ---
if [[ "$TARGET" == "benchmark" ]]; then
    echo -e "${YELLOW}[BENCHMARK] Running benchmarks...${NC}"
    dotnet run -c Release --project tests/Loco.Tests/Loco.Tests.csproj --filter "*Benchmark*"
    exit $?
fi

# --- Target: test ---
if [[ "$TARGET" == "test" ]]; then
    echo -e "${YELLOW}[TEST] Running tests...${NC}"
    dotnet test -c Release --nologo --logger:"console;verbosity=normal"
    exit $?
fi

# --- Target: publish ---
if [[ "$TARGET" == "publish" ]]; then
    echo -e "${YELLOW}[PUBLISH] Creating production release package...${NC}"

    OUTPUT_DIR="publish-production"
    BUILD_CONFIG="Release"
    RUNTIME=""
    case "$OS" in
        Linux) RUNTIME="linux-x64" ;;
        macOS)
            if [[ $(uname -m) == "arm64" ]]; then RUNTIME="osx-arm64"; else RUNTIME="osx-x64"; fi ;;
        *) RUNTIME="linux-x64" ;;
    esac

    echo "[1/8] Cleaning previous builds..."
    rm -rf "$OUTPUT_DIR" 2>/dev/null || true
    rm -rf "src/Loco.Cli/bin/$BUILD_CONFIG" 2>/dev/null || true
    rm -rf "src/Loco.Core/bin/$BUILD_CONFIG" 2>/dev/null || true

    echo "[2/8] Restoring NuGet packages..."
    dotnet restore --verbosity quiet

    echo "[3/8] Building solution..."
    dotnet build --configuration "$BUILD_CONFIG" --no-restore --verbosity quiet

    echo "[4/8] Running tests..."
    dotnet test --configuration "$BUILD_CONFIG" --no-build --verbosity quiet --logger "console;verbosity=minimal"

    echo "[5/8] Publishing CLI for $RUNTIME..."
    dotnet publish src/Loco.Cli/Loco.Cli.csproj \
        --configuration "$BUILD_CONFIG" \
        --runtime "$RUNTIME" \
        --self-contained true \
        --output "$OUTPUT_DIR" \
        /p:PublishSingleFile=true \
        /p:PublishReadyToRun=true \
        /p:PublishTrimmed=false \
        /p:IncludeNativeLibrariesForSelfExtract=true \
        /p:DebugType=None \
        /p:DebugSymbols=false \
        --verbosity quiet

    echo "[6/8] Creating package structure..."
    mkdir -p "$OUTPUT_DIR/config"
    mkdir -p "$OUTPUT_DIR/workflows"

    # Copy documentation
    cp README.md "$OUTPUT_DIR/" 2>/dev/null || true
    cp QUICK_START.md "$OUTPUT_DIR/" 2>/dev/null || true
    cp GETTING_STARTED.md "$OUTPUT_DIR/" 2>/dev/null || true
    cp FAQ.md "$OUTPUT_DIR/" 2>/dev/null || true
    cp TROUBLESHOOTING.md "$OUTPUT_DIR/" 2>/dev/null || true

    # Copy installation scripts
    cp install.sh "$OUTPUT_DIR/" 2>/dev/null || true
    chmod +x "$OUTPUT_DIR/install.sh" 2>/dev/null || true

    # Copy example workflows
    cp workflows/*.json "$OUTPUT_DIR/workflows/" 2>/dev/null || true

    # Make CLI executable
    chmod +x "$OUTPUT_DIR/Loco.Cli" 2>/dev/null || true

    echo "[7/8] Creating version information..."
    cat > "$OUTPUT_DIR/build-info.json" << EOF
{
  "version": "1.0.0",
  "buildDate": "$(date '+%Y-%m-%d %H:%M:%S')",
  "buildConfiguration": "$BUILD_CONFIG",
  "runtime": "$RUNTIME",
  "features": [
    "Self-Contained",
    "Single-File",
    "ReadyToRun",
    "Production-Optimized"
  ]
}
EOF

    echo "[8/8] Verifying build..."
    "$OUTPUT_DIR/Loco.Cli" --version >/dev/null 2>&1 || {
        echo -e "${RED}ERROR: Build verification failed${NC}"
        exit 1
    }

    echo -e "\n${GREEN}=====================================${NC}"
    echo -e "${GREEN}     Build Complete!${NC}"
    echo -e "${GREEN}=====================================${NC}"
    echo "Platform: $OS ($RUNTIME)"
    echo "Output: $OUTPUT_DIR/"
    echo "Executable: $OUTPUT_DIR/Loco.Cli"
    echo ""
    echo "Verification:"
    echo "  ./$OUTPUT_DIR/Loco.Cli --version"
    echo "  ./$OUTPUT_DIR/Loco.Cli health"
    exit 0
fi

# --- Target: docker ---
if [[ "$TARGET" == "docker" ]]; then
    echo -e "${YELLOW}[DOCKER] Building Docker image...${NC}"
    docker build -t loco:latest .
    exit $?
fi

# --- Target: web ---
if [[ "$TARGET" == "web" ]]; then
    echo -e "${YELLOW}[WEB] Building Web UI...${NC}"
    if [ ! -f "web/package.json" ]; then
        echo "[INFO] Web project not found, skipping."
        exit 0
    fi
    (cd web && npm install && npm run build)
    if [ $? -ne 0 ]; then echo -e "${RED}[ERROR] Web build failed${NC}"; exit 1; fi
    echo -e "${GREEN}[SUCCESS] Web UI built.${NC}"
    exit 0
fi

# --- Target: mobile ---
if [[ "$TARGET" == "mobile" ]]; then
    echo -e "${YELLOW}[MOBILE] Preparing Mobile App...${NC}"
    if [ ! -f "mobile/package.json" ]; then
        echo "[INFO] Mobile project not found, skipping."
        exit 0
    fi
    (cd mobile && npm install)
    if [ $? -ne 0 ]; then echo -e "${RED}[ERROR] npm install failed${NC}"; exit 1; fi
    echo -e "${GREEN}[SUCCESS] Mobile app dependencies installed.${NC}"
    echo "[INFO] To build the app, run 'npx expo build:[android|ios]' in the 'mobile' directory."
    exit 0
fi

# --- Target: all ---
if [[ "$TARGET" == "all" ]]; then
    $0 clean && $0 build && $0 test && $0 publish && $0 web && $0 mobile && $0 docker
    exit $?
fi

# --- Unknown target ---
echo -e "${RED}Unknown target: $TARGET${NC}"
echo
echo "Usage: build.sh [target]"
echo
echo "Targets:"
echo "  build    - Build the solution (default)"
echo "  test     - Run unit tests"
echo "  benchmark- Run benchmark tests"
echo "  publish  - Create release package"
echo "  web      - Build Web UI"
echo "  mobile   - Prepare Mobile App (install dependencies)"
echo "  docker   - Build Docker image"
echo "  clean    - Clean build artifacts"
echo "  all      - Run all targets (clean, build, test, publish, web, mobile, docker)"
echo
exit 1
