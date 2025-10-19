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
- Validates 7 workflow JSON files
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

## Typical Workflows

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
# Build everything
scripts\build-all.bat

# Validate examples
scripts\run-examples.bat

# Create production build
scripts\publish.bat

# Test production build
cd publish
Loco.Cli.exe --help
Loco.Cli.exe health
```

### CI/CD Pipeline
```batch
# Full verification
scripts\build-all.bat && scripts\run-examples.bat
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
