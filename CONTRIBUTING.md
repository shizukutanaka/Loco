# Contributing to Loco

Thank you for your interest in contributing to Loco! This document provides guidelines and instructions for contributing.

## Code of Conduct

By participating in this project, you agree to maintain a respectful and inclusive environment for all contributors.

## How to Contribute

### Reporting Issues

1. Check existing issues to avoid duplicates
2. Use the issue template when available
3. Provide clear reproduction steps
4. Include system information (OS, .NET version, etc.)

### Pull Requests

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/your-feature`)
3. Make your changes following our coding standards
4. Write or update tests as needed
5. Update documentation
6. Commit with clear messages
7. Push to your fork
8. Submit a pull request

### Development Setup

```bash
# Clone the repository
git clone https://github.com/yourusername/loco.git
cd loco

# Build the project
dotnet build

# Run tests
dotnet test

# Run the application
dotnet run --project src/Loco.Cli
```

## Coding Standards

### C# Code Style

- Use 4 spaces for indentation (no tabs)
- Follow Microsoft C# coding conventions
- Use meaningful variable and method names
- Add XML documentation comments for public APIs
- Keep methods focused and under 50 lines when possible

### Commit Messages

Format: `type(scope): description`

Types:
- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation changes
- `style`: Code style changes
- `refactor`: Code refactoring
- `test`: Test additions or changes
- `chore`: Build process or auxiliary tool changes

Example: `feat(automation): add natural language rule support`

### Testing

- Write unit tests for new functionality
- Ensure all tests pass before submitting PR
- Aim for at least 80% code coverage
- Use descriptive test names

### Documentation

- Update README.md for user-facing changes
- Add XML comments for public APIs
- Update relevant documentation files
- Include examples for new features

## Security

### Reporting Security Issues

DO NOT create public issues for security vulnerabilities. Instead:

1. Email security concerns to the maintainers
2. Include detailed description and reproduction steps
3. Allow time for a fix before public disclosure

### Security Requirements

- Never commit secrets, keys, or passwords
- Use secure coding practices
- Validate all user inputs
- Follow OWASP guidelines
- Implement proper error handling

## Project Structure

```
loco/
├── src/
│   ├── Loco.Core/          # Core functionality
│   ├── Loco.Cli/           # CLI application
│   ├── Loco.Web/           # Web interface
│   └── Loco.Automation/    # Automation engine
├── tests/                   # Test projects
├── docs/                    # Documentation
└── examples/               # Example configurations
```

## Building and Testing

### Prerequisites

- .NET 8.0 SDK or later
- Node.js 18+ (for web UI)
- Visual Studio 2022 or VS Code (recommended)

### Build Commands

```bash
# Build all projects
dotnet build

# Build in release mode
dotnet build -c Release

# Run specific project
dotnet run --project src/Loco.Cli

# Run tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

## Release Process

1. Update version in `Directory.Build.props`
2. Update CHANGELOG.md
3. Create release branch
4. Run full test suite
5. Build release artifacts
6. Create GitHub release
7. Publish NuGet packages

## Community

### Getting Help

- Check documentation first
- Search existing issues
- Ask questions in discussions
- Join our community channels

### Recognition

Contributors will be recognized in:
- CONTRIBUTORS.md file
- Release notes
- Project documentation

## License

By contributing, you agree that your contributions will be licensed under the same license as the project (MIT License).

## Questions?

If you have questions about contributing, please:
1. Check this guide first
2. Look for answers in existing issues
3. Create a new discussion if needed

Thank you for contributing to Loco!