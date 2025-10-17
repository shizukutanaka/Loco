#!/bin/bash

# Loco Cross-Platform Installer
# One-line install: curl -sSL https://raw.githubusercontent.com/shizukutanaka/Loco/main/install.sh | bash

set -euo pipefail
IFS=$'\n\t'

# Configuration
REPO_OWNER="shizukutanaka"
REPO_NAME="Loco"
REPO_URL="https://github.com/${REPO_OWNER}/${REPO_NAME}"
INSTALL_DIR="$HOME/.loco"
BIN_DIR="$HOME/.local/bin"
VERSION="latest"

# Globals populated at runtime
OS="unknown"
ARCH="unknown"
RUNTIME=""
TMP_DIR=""
SRC_DIR=""

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

SELF_CONTAINED=true
CLI_BINARY=""
SHELL_RC=""
BIN_WRAPPER=""

log_step() {
    printf "%b==> %s%b\n" "$BLUE" "$1" "$NC"
}

log_info() {
    printf "%b[INFO]%b %s\n" "$BLUE" "$NC" "$1"
}

log_warn() {
    printf "%b[WARN]%b %s\n" "$YELLOW" "$NC" "$1"
}

log_error() {
    printf "%b[ERROR]%b %s\n" "$RED" "$NC" "$1"
}

fail() {
    log_error "$1"
    exit 1
}

detect_platform() {
    case "$(uname -s)" in
        Linux*)  OS="linux";;
        Darwin*) OS="macos";;
        CYGWIN*|MINGW*|MSYS*) OS="windows";;
        *)       fail "Unsupported operating system: $(uname -s)";;
    esac

    case "$(uname -m)" in
        x86_64|amd64) ARCH="x64";;
        arm64|aarch64) ARCH="arm64";;
        armv7l|armhf) ARCH="arm";;
        *) fail "Unsupported architecture: $(uname -m)";;
    esac

    case "$OS-$ARCH" in
        linux-x64)   RUNTIME="linux-x64";;
        linux-arm64) RUNTIME="linux-arm64";;
        linux-arm)   RUNTIME="linux-arm";;
        macos-x64)   RUNTIME="osx-x64";;
        macos-arm64) RUNTIME="osx-arm64";;
        windows-x64) RUNTIME="win-x64";;
        windows-arm64) RUNTIME="win-arm64";;
        *) fail "Unsupported runtime combination: $OS-$ARCH";;
    esac

    log_info "Detected platform $OS ($ARCH), runtime identifier $RUNTIME"
}

ensure_tools() {
    log_step "Checking prerequisites"

    if command -v curl >/dev/null 2>&1; then
        DOWNLOAD_CMD="curl"
    elif command -v wget >/dev/null 2>&1; then
        DOWNLOAD_CMD="wget"
    else
        fail "curl or wget is required"
    fi

    command -v tar >/dev/null 2>&1 || fail "tar is required"
    command -v dotnet >/dev/null 2>&1 || fail "dotnet SDK 8.0 or later is required"

    if ! dotnet --list-sdks | grep -q "^8\."; then
        log_warn "dotnet 8 SDK not detected. Continuing but build may fail."
    fi
}

prepare_shell_rc() {
    if [[ -n "${ZSH_VERSION:-}" ]]; then
        SHELL_RC="$HOME/.zshrc"
    elif [[ -n "${BASH_VERSION:-}" ]]; then
        SHELL_RC="$HOME/.bashrc"
    elif [[ -f "$HOME/.profile" ]]; then
        SHELL_RC="$HOME/.profile"
    else
        SHELL_RC=""
    fi
}

create_tmpdir() {
    TMP_DIR=$(mktemp -d 2>/dev/null || mktemp -d -t loco)
    SRC_DIR="$TMP_DIR/src"
    mkdir -p "$SRC_DIR"
}

download_file() {
    local url="$1"
    local dest="$2"

    case "$DOWNLOAD_CMD" in
        curl)
            curl -fL "$url" -o "$dest" || fail "Failed to download $url"
            ;;
        wget)
            wget --https-only -qO "$dest" "$url" || fail "Failed to download $url"
            ;;
    esac
}

download_source() {
    log_step "Fetching source code"
    local ref="$VERSION"
    local tarball=""

    if [[ "$ref" == "latest" ]]; then
        ref="main"
        tarball="https://codeload.github.com/${REPO_OWNER}/${REPO_NAME}/tar.gz/refs/heads/${ref}"
    else
        tarball="https://codeload.github.com/${REPO_OWNER}/${REPO_NAME}/tar.gz/refs/tags/${ref}"
    fi

    local archive="$TMP_DIR/source.tar.gz"
    download_file "$tarball" "$archive"
    tar -xzf "$archive" -C "$TMP_DIR"

    local extracted
    extracted=$(find "$TMP_DIR" -maxdepth 1 -mindepth 1 -type d -name "${REPO_NAME}-*" | head -n 1)
    [[ -d "$extracted" ]] || fail "Failed to extract repository"
    cp -R "$extracted"/. "$SRC_DIR"/
}

publish_cli() {
    log_step "Building Loco CLI (runtime: $RUNTIME)"
    local publish_dir="$TMP_DIR/publish"
    mkdir -p "$publish_dir"

    local publish_args=(publish "$SRC_DIR/src/Loco.Cli" -c Release -r "$RUNTIME" -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "$publish_dir")

    if [[ "$SELF_CONTAINED" == true ]]; then
        publish_args+=(--self-contained true)
    else
        publish_args+=(--self-contained false)
    fi

    dotnet "${publish_args[@]}"

    CLI_BINARY="$publish_dir/Loco.Cli"
    [[ "$OS" == "windows" ]] && CLI_BINARY+=".exe"
    [[ -f "$CLI_BINARY" ]] || fail "Build completed but Loco CLI binary not found"
}

install_cli() {
    log_step "Installing into $INSTALL_DIR"
    local target_bin_dir="$INSTALL_DIR/bin"

    mkdir -p "$target_bin_dir"
    mkdir -p "$BIN_DIR"

    install -m 755 "$CLI_BINARY" "$target_bin_dir/"

    BIN_WRAPPER="$target_bin_dir/loco"
    cat > "$BIN_WRAPPER" <<'EOF'
#!/bin/sh
set -e
SCRIPT_DIR="$(CDPATH= cd -- "$(dirname "$0")" && pwd)"
exec "$SCRIPT_DIR/Loco.Cli" "$@"
EOF
    chmod 755 "$BIN_WRAPPER"

    if [[ "$OS" == "windows" ]]; then
        cat > "$target_bin_dir/loco.cmd" <<'EOF'
@echo off
"%~dp0Loco.Cli.exe" %*
EOF
    fi

    ln -sf "$BIN_WRAPPER" "$BIN_DIR/loco"
}

configure_environment() {
    log_step "Configuring environment"

    prepare_shell_rc

    if [[ -n "$SHELL_RC" ]]; then
        if ! grep -Fq "$BIN_DIR" "$SHELL_RC" 2>/dev/null; then
            {
                printf "\n# Loco automation platform\n"
                printf "export PATH=\"$BIN_DIR:\$PATH\"\n"
            } >> "$SHELL_RC"
            log_info "Updated PATH in $SHELL_RC"
        else
            log_info "PATH already includes $BIN_DIR"
        fi
    else
        log_warn "Could not detect shell profile file automatically. Ensure $BIN_DIR is on your PATH."
    fi

    mkdir -p "$HOME/.config/loco"
    cat > "$HOME/.config/loco/config.json" <<EOF
{
  "version": "${VERSION}",
  "runtime": "${RUNTIME}",
  "installDir": "${INSTALL_DIR}",
  "binDir": "${BIN_DIR}",
  "selfContained": ${SELF_CONTAINED}
}
EOF
}

verify_installation() {
    log_step "Verifying installation"

    if "$BIN_DIR/loco" --version >/dev/null 2>&1; then
        log_info "Loco CLI is operational"
    else
        fail "Verification failed. Check $BIN_DIR/loco --version output"
    fi
}

cleanup() {
    [[ -n "$TMP_DIR" && -d "$TMP_DIR" ]] && rm -rf "$TMP_DIR"
}

print_summary() {
    echo
    printf "%b=====================================%b\n" "$GREEN" "$NC"
    printf "%b Installation Complete%b\n" "$GREEN" "$NC"
    printf "%b=====================================%b\n" "$GREEN" "$NC"
    echo
    printf "Install directory : %s\n" "$INSTALL_DIR"
    printf "User binary path  : %s/loco\n" "$BIN_DIR"
    if [[ -n "$SHELL_RC" ]]; then
        printf "Reload shell config: source %s\n" "$SHELL_RC"
    fi
    printf "Check CLI: %s\n" "$BIN_DIR/loco --help"
}

parse_args() {
    while [[ $# -gt 0 ]]; do
        case "$1" in
            --version)
                [[ $# -lt 2 ]] && fail "--version requires an argument"
                VERSION="$2"
                shift 2
                ;;
            --dir)
                [[ $# -lt 2 ]] && fail "--dir requires an argument"
                INSTALL_DIR="$2"
                shift 2
                ;;
            --bin-dir)
                [[ $# -lt 2 ]] && fail "--bin-dir requires an argument"
                BIN_DIR="$2"
                shift 2
                ;;
            --framework-dependent)
                SELF_CONTAINED=false
                shift
                ;;
            --help)
                cat <<EOF
Usage: $0 [OPTIONS]

Options:
  --version <tag>            Install a specific tag (default: latest main branch)
  --dir <path>               Installation directory (default: $HOME/.loco)
  --bin-dir <path>           Directory for the loco shim (default: $HOME/.local/bin)
  --framework-dependent      Produce a framework-dependent build (requires .NET runtime)
  --help                     Show this help message
EOF
                exit 0
                ;;
            *)
                fail "Unknown option: $1"
                ;;
        esac
    done
}

main() {
    parse_args "$@"
    trap cleanup EXIT

    detect_platform
    ensure_tools
    create_tmpdir
    download_source
    publish_cli
    install_cli
    configure_environment
    verify_installation
    print_summary
}

main "$@"
