#!/bin/bash

# Loco Cross-Platform Installer
# One-line install: curl -sSL https://loco.dev/install.sh | bash

set -e

# Configuration
REPO_URL="https://github.com/loco/loco"
INSTALL_DIR="$HOME/.loco"
BIN_DIR="$HOME/.local/bin"
VERSION="latest"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

# Detect OS and Architecture
detect_platform() {
    OS="unknown"
    ARCH="unknown"
    
    # Detect OS
    case "$(uname -s)" in
        Linux*)     OS="linux";;
        Darwin*)    OS="macos";;
        CYGWIN*|MINGW*|MSYS*) OS="windows";;
        *)          OS="unknown";;
    esac
    
    # Detect Architecture
    case "$(uname -m)" in
        x86_64|amd64)   ARCH="x64";;
        arm64|aarch64)  ARCH="arm64";;
        armv7l|armhf)   ARCH="arm";;
        *)              ARCH="unknown";;
    esac
    
    # Special case for macOS M1/M2
    if [[ "$OS" == "macos" && "$ARCH" == "arm64" ]]; then
        RUNTIME="osx-arm64"
    elif [[ "$OS" == "macos" ]]; then
        RUNTIME="osx-x64"
    elif [[ "$OS" == "linux" && "$ARCH" == "arm64" ]]; then
        RUNTIME="linux-arm64"
    elif [[ "$OS" == "linux" && "$ARCH" == "arm" ]]; then
        RUNTIME="linux-arm"
    elif [[ "$OS" == "linux" ]]; then
        RUNTIME="linux-x64"
    else
        echo -e "${RED}Unsupported platform: $OS-$ARCH${NC}"
        exit 1
    fi
    
    echo -e "${GREEN}Detected platform: $OS-$ARCH ($RUNTIME)${NC}"
}

# Check prerequisites
check_prerequisites() {
    echo -e "${YELLOW}Checking prerequisites...${NC}"
    
    # Check for curl or wget
    if ! command -v curl &> /dev/null && ! command -v wget &> /dev/null; then
        echo -e "${RED}ERROR: curl or wget is required${NC}"
        exit 1
    fi
    
    # Check for tar
    if ! command -v tar &> /dev/null; then
        echo -e "${RED}ERROR: tar is required${NC}"
        exit 1
    fi
    
    # Optional: Check for .NET Runtime (if not using self-contained)
    if command -v dotnet &> /dev/null; then
        echo -e "${GREEN}✓ .NET Runtime found${NC}"
    else
        echo -e "${YELLOW}⚠ .NET Runtime not found (using self-contained version)${NC}"
    fi
}

# Download Loco
download_loco() {
    echo -e "${YELLOW}Downloading Loco $VERSION for $RUNTIME...${NC}"
    
    # Determine download URL
    if [[ "$VERSION" == "latest" ]]; then
        DOWNLOAD_URL="$REPO_URL/releases/latest/download/loco-$RUNTIME.tar.gz"
    else
        DOWNLOAD_URL="$REPO_URL/releases/download/$VERSION/loco-$RUNTIME.tar.gz"
    fi
    
    # Create temp directory
    TMP_DIR=$(mktemp -d)
    cd "$TMP_DIR"
    
    # Download
    if command -v curl &> /dev/null; then
        curl -sSL "$DOWNLOAD_URL" -o loco.tar.gz
    else
        wget -q "$DOWNLOAD_URL" -O loco.tar.gz
    fi
    
    # Extract
    tar -xzf loco.tar.gz
    
    echo -e "${GREEN}✓ Download complete${NC}"
}

# Install Loco
install_loco() {
    echo -e "${YELLOW}Installing Loco...${NC}"
    
    # Create directories
    mkdir -p "$INSTALL_DIR"
    mkdir -p "$BIN_DIR"
    
    # Copy files
    cp -r * "$INSTALL_DIR/" 2>/dev/null || true
    
    # Create symlink
    ln -sf "$INSTALL_DIR/loco" "$BIN_DIR/loco"
    
    # Make executable
    chmod +x "$INSTALL_DIR/loco"
    chmod +x "$INSTALL_DIR/Loco.Cli" 2>/dev/null || true
    
    echo -e "${GREEN}✓ Installation complete${NC}"
}

# Setup environment
setup_environment() {
    echo -e "${YELLOW}Setting up environment...${NC}"
    
    # Detect shell
    SHELL_RC=""
    if [[ -n "$ZSH_VERSION" ]]; then
        SHELL_RC="$HOME/.zshrc"
    elif [[ -n "$BASH_VERSION" ]]; then
        SHELL_RC="$HOME/.bashrc"
    elif [[ -f "$HOME/.profile" ]]; then
        SHELL_RC="$HOME/.profile"
    fi
    
    # Add to PATH if not already there
    if [[ -n "$SHELL_RC" ]]; then
        if ! grep -q "$BIN_DIR" "$SHELL_RC"; then
            echo "" >> "$SHELL_RC"
            echo "# Loco automation platform" >> "$SHELL_RC"
            echo "export PATH=\"\$PATH:$BIN_DIR\"" >> "$SHELL_RC"
            echo -e "${GREEN}✓ Added to PATH in $SHELL_RC${NC}"
            echo -e "${YELLOW}  Run: source $SHELL_RC${NC}"
        else
            echo -e "${GREEN}✓ PATH already configured${NC}"
        fi
    fi
    
    # Create config directory
    mkdir -p "$HOME/.config/loco"
    
    # Create default config
    cat > "$HOME/.config/loco/config.json" << EOF
{
    "version": "$VERSION",
    "runtime": "$RUNTIME",
    "installDir": "$INSTALL_DIR",
    "language": "en",
    "theme": "dark",
    "autoUpdate": true
}
EOF
    
    echo -e "${GREEN}✓ Configuration created${NC}"
}

# Install dependencies based on OS
install_dependencies() {
    echo -e "${YELLOW}Checking system dependencies...${NC}"
    
    case "$OS" in
        linux)
            # Check for notification support
            if ! command -v notify-send &> /dev/null; then
                echo -e "${YELLOW}  notify-send not found (notifications will use fallback)${NC}"
            fi
            
            # Check for TTS support
            if ! command -v espeak &> /dev/null && ! command -v festival &> /dev/null; then
                echo -e "${YELLOW}  TTS not available (install espeak or festival for text-to-speech)${NC}"
            fi
            ;;
        
        macos)
            # macOS has built-in support for notifications and TTS
            echo -e "${GREEN}✓ macOS native features available${NC}"
            ;;
    esac
}

# Verify installation
verify_installation() {
    echo -e "${YELLOW}Verifying installation...${NC}"
    
    if "$BIN_DIR/loco" --version &> /dev/null; then
        echo -e "${GREEN}✓ Loco installed successfully!${NC}"
        "$BIN_DIR/loco" --version
    else
        echo -e "${RED}✗ Installation verification failed${NC}"
        exit 1
    fi
}

# Cleanup
cleanup() {
    if [[ -n "$TMP_DIR" && -d "$TMP_DIR" ]]; then
        rm -rf "$TMP_DIR"
    fi
}

# Main installation flow
main() {
    echo -e "${BLUE}=====================================${NC}"
    echo -e "${BLUE}     Loco Installer${NC}"
    echo -e "${BLUE}=====================================${NC}"
    echo
    
    # Parse arguments
    while [[ $# -gt 0 ]]; do
        case $1 in
            --version)
                VERSION="$2"
                shift 2
                ;;
            --dir)
                INSTALL_DIR="$2"
                shift 2
                ;;
            --help)
                echo "Usage: $0 [OPTIONS]"
                echo "Options:"
                echo "  --version VERSION  Install specific version (default: latest)"
                echo "  --dir DIRECTORY    Installation directory (default: ~/.loco)"
                echo "  --help            Show this help message"
                exit 0
                ;;
            *)
                echo -e "${RED}Unknown option: $1${NC}"
                exit 1
                ;;
        esac
    done
    
    # Set trap for cleanup
    trap cleanup EXIT
    
    # Run installation steps
    detect_platform
    check_prerequisites
    download_loco
    install_loco
    setup_environment
    install_dependencies
    verify_installation
    
    echo
    echo -e "${GREEN}=====================================${NC}"
    echo -e "${GREEN}     Installation Complete!${NC}"
    echo -e "${GREEN}=====================================${NC}"
    echo
    echo -e "Loco has been installed to: ${BLUE}$INSTALL_DIR${NC}"
    echo -e "Binary location: ${BLUE}$BIN_DIR/loco${NC}"
    echo
    echo -e "${YELLOW}Next steps:${NC}"
    echo -e "  1. Reload your shell: ${BLUE}source $SHELL_RC${NC}"
    echo -e "  2. Run Loco: ${BLUE}loco --help${NC}"
    echo -e "  3. Create your first flow: ${BLUE}loco build${NC}"
    echo
    echo -e "${GREEN}Happy automating! 🚀${NC}"
}

# Run main function
main "$@"
