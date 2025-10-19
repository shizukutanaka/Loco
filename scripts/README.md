# Scripts Directory

This directory contains automation scripts for building, testing, and deploying the Loco CLI application.

## Available Scripts

### 🔨 build-all.bat
**Complete build and test workflow**

Performs a full clean build with all quality checks:
- Cleans previous builds
- Restores NuGet dependencies
- Builds in Release configuration
- Runs all unit tests (103 tests)

**Usage:**
```batch
scripts\build-all.bat
```

**Output:** `src\Loco.Cli\bin\Release\net8.0\`

---

### 📦 publish.bat
**Create production-ready build**

Creates a self-contained, single-file executable for distribution:
- Builds for Windows x64
- Creates self-contained executable (no .NET runtime required)
- Includes all examples and documentation
- Generates version information file

**Usage:**
```batch
scripts\publish.bat
```

**Output:** `publish\` directory with:
- `Loco.Cli.exe` - Self-contained executable
- `examples\` - All workflow and IaC samples
- `README.md`, `QUICKSTART.md` - Documentation
- `VERSION.txt` - Build information

**Distribution:**
The `publish` directory can be zipped and distributed to users who don't have .NET installed.

---

### ⚡ quick-test.bat
**Fast verification for development**

Quick validation during development:
- Fast Release build
- Runs all unit tests (103 tests)
- Functional tests (version, health, help commands)

**Usage:**
```batch
scripts\quick-test.bat
```

**Perfect for:**
- Pre-commit verification
- Quick iteration during development
- CI/CD pipeline health checks

---

### ✅ run-examples.bat
**Validate all example files**

Tests all workflow and IaC examples:
- Validates 6 workflow JSON files
- Validates 4 IaC YAML files
- Ensures examples load correctly
- Verifies visualization works

**Usage:**
```batch
scripts\run-examples.bat
```

**Tests:**
- `examples/workflows/system-monitoring.json`
- `examples/workflows/daily-backup.json`
- `examples/workflows/parallel-processing.json`
- `examples/workflows/log-cleanup.json`
- `examples/workflows/database-backup.json`
- `examples/workflows/dev-environment-setup.json`
- All IaC files in `examples/iac/`

---

### 🚀 ci-build.bat
**CI/CD pipeline build script**

Designed for automated build systems (GitHub Actions, Azure DevOps, Jenkins):
- Complete clean, restore, build, test cycle
- Returns proper exit codes for CI/CD
- Optional packaging with `--package` flag
- Minimal output for CI logs

**Usage:**
```batch
# Standard CI build
scripts\ci-build.bat

# CI build with packaging
scripts\ci-build.bat --package
```

**Exit Codes:**
- `0` - Build and tests succeeded
- `1` - Build or tests failed

**Perfect for:** GitHub Actions, Azure Pipelines, Jenkins, GitLab CI

---

### 📋 prepare-release.bat
**Complete release package preparation**

Creates a production-ready distribution package:
- Full build and test verification
- Self-contained executable
- Documentation and examples included
- Version information file
- Ready-to-distribute folder structure

**Usage:**
```batch
scripts\prepare-release.bat [version]

# Example:
scripts\prepare-release.bat 0.1.0
```

**Output:** `release-v[version]\` directory with:
- `bin\Loco.Cli.exe` - Self-contained executable
- `examples\` - All samples
- `docs\` - Documentation
- `README.md`, `QUICKSTART.md`
- `VERSION.txt` - Build metadata

**Next steps after running:**
1. Test: `cd release-v[version]\bin && Loco.Cli.exe version`
2. Archive: `tar -czf loco-v[version]-win-x64.tar.gz release-v[version]`
3. Publish to GitHub Releases

---

### 🛠️ dev-setup.bat
**Development environment setup**

One-command setup for new developers:
- Verifies .NET SDK installation
- Checks Git availability
- Restores all dependencies
- Builds and tests project
- Creates workspace directories

**Usage:**
```batch
scripts\dev-setup.bat
```

**Creates:**
- `workflows\` - Workflow storage
- `rules\` - Rule storage
- `logs\` - Log files
- `backups\` - Backup storage

**Perfect for:** Onboarding new contributors, fresh git clone setup

---

## Typical Workflows

### Initial Setup (New Developers)
```batch
# One-command environment setup
scripts\dev-setup.bat

# Verify setup
scripts\quick-test.bat
```

### Development Workflow
```batch
# During development
scripts\quick-test.bat

# Before committing
scripts\build-all.bat
scripts\run-examples.bat
```

### Release Workflow
```batch
# Prepare complete release package
scripts\prepare-release.bat 0.1.0

# Test release
cd release-v0.1.0\bin
Loco.Cli.exe version
Loco.Cli.exe health
cd ..\..

# Create archive for distribution
tar -czf loco-v0.1.0-win-x64.tar.gz release-v0.1.0
```

### CI/CD Pipeline
```batch
# GitHub Actions / Azure DevOps / Jenkins
scripts\ci-build.bat

# With packaging
scripts\ci-build.bat --package

# Full verification with examples
scripts\ci-build.bat && scripts\run-examples.bat
```

---

## Script Requirements

All scripts require:
- **.NET 8.0 SDK** installed
- Windows operating system
- Run from project root or scripts directory

Scripts automatically:
- Change to project root directory
- Handle errors with appropriate exit codes
- Provide progress feedback
- Show colored output for readability

---

## Exit Codes

All scripts follow standard exit code conventions:
- `0` - Success
- `1` - Build/test failure
- Other - Specific error conditions

Perfect for automated CI/CD pipelines!

---

## Troubleshooting

### Build fails with "dotnet command not found"
- Install .NET 8.0 SDK from https://dotnet.microsoft.com/download

### Tests fail
- Run `scripts\build-all.bat` for detailed error messages
- Check `src\Loco.Cli\bin\Release\net8.0\` for build output

### Publish creates large executable
- This is normal for self-contained builds (~70MB)
- Includes entire .NET runtime for portability
- Users don't need .NET installed

---

## Adding New Scripts

When creating new scripts:
1. Use `.bat` extension for Windows
2. Include header comment with description
3. Change to project root: `cd /d "%~dp0.."`
4. Return proper exit codes
5. Provide progress feedback
6. Update this README

---

## License

These scripts are part of the Loco project and share the same license.
