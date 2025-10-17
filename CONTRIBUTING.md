# Contributing to Loco Automation Platform

Thank you for considering contributing to Loco! This document provides guidelines and instructions for contributing.

## Code of Conduct

### Our Standards

- Be respectful and inclusive
- Welcome constructive feedback
- Focus on what is best for the community
- Show empathy towards other community members

### Our Responsibilities

Project maintainers are responsible for clarifying standards of acceptable behavior and will take appropriate and fair corrective action in response to any instances of unacceptable behavior.

## How to Contribute

### Reporting Bugs

Before creating bug reports, please check existing issues to avoid duplicates. When creating a bug report, include:

- **Clear title and description**
- **Steps to reproduce** the behavior
- **Expected behavior**
- **Actual behavior**
- **Screenshots** (if applicable)
- **Environment details**:
  - OS version
  - .NET version
  - Loco version

**Example Bug Report:**

```markdown
### Bug: Command execution fails with special characters

**Environment:**
- OS: Windows 11
- .NET: 8.0.1
- Loco: 1.0.0

**Steps to Reproduce:**
1. Create rule with command: `echo "test's"`
2. Execute rule with `loco exec <rule-id>`
3. Observe error

**Expected:** Command executes successfully
**Actual:** ArgumentException: Invalid character in argument

**Additional Context:** Works with `echo test` without special characters
```

### Suggesting Enhancements

Enhancement suggestions are tracked as GitHub issues. When creating an enhancement suggestion, include:

- **Clear title and description**
- **Rationale** for the enhancement
- **Expected behavior** after implementation
- **Possible implementation** approach (optional)
- **Impact assessment** (breaking changes, performance)

### Pull Requests

1. **Fork the repository** and create your branch from `main`
2. **Follow code style** guidelines below
3. **Add tests** for new functionality
4. **Update documentation** as needed
5. **Ensure all tests pass**
6. **Write clear commit messages**
7. **Submit the pull request**

## Development Setup

### Prerequisites

- .NET 8 SDK or later
- Git
- A code editor (Visual Studio, VS Code, or Rider recommended)

### Getting Started

```bash
# Clone your fork
git clone https://github.com/YOUR-USERNAME/Loco.git
cd Loco

# Build the solution
dotnet build

# Run tests
dotnet test

# Run the CLI
dotnet run --project src/Loco.Cli
```

### Project Structure

```
Loco/
├── src/
│   ├── Loco.Core/          # Core automation engine
│   ├── Loco.Cli/           # Command-line interface
│   └── Loco.LLM/           # LLM integration (optional)
├── tests/
│   ├── Loco.Core.Tests/    # Core engine tests
│   └── Loco.Cli.Tests/     # CLI tests
├── docs/                    # Documentation
└── examples/                # Example workflows
```

## Code Style Guidelines

### C# Style

#### General Principles

- Follow [Microsoft C# Coding Conventions](https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- Use nullable reference types (`#nullable enable`)
- Enable `TreatWarningsAsErrors` in development
- Target .NET 8.0

#### Naming Conventions

```csharp
// ✅ Good
public class WorkflowEngine { }
private readonly ILogger _logger;
public string WorkflowId { get; set; }
private const int MaxRetries = 3;

// ❌ Bad
public class workflowEngine { }
private readonly ILogger logger;
public string workflowID { get; set; }
private const int max_retries = 3;
```

#### Async/Await Patterns

```csharp
// ✅ Good - Use ConfigureAwait(false) in library code
public async Task<Result> ProcessAsync(CancellationToken ct)
{
    var data = await FetchDataAsync(ct).ConfigureAwait(false);
    return await ProcessDataAsync(data, ct).ConfigureAwait(false);
}

// ❌ Bad - Missing ConfigureAwait
public async Task<Result> ProcessAsync(CancellationToken ct)
{
    var data = await FetchDataAsync(ct);
    return await ProcessDataAsync(data, ct);
}

// ❌ Bad - Using .Result (deadlock risk)
public Result Process()
{
    return ProcessAsync(CancellationToken.None).Result;
}
```

#### Exception Handling

```csharp
// ✅ Good - Specific exceptions with context
public void ValidateConfig(Config config)
{
    if (string.IsNullOrEmpty(config.WorkflowId))
    {
        throw new ValidationException(
            "Workflow ID is required",
            new[] { "WorkflowId cannot be null or empty" });
    }
}

// ❌ Bad - Generic exceptions
public void ValidateConfig(Config config)
{
    if (string.IsNullOrEmpty(config.WorkflowId))
    {
        throw new Exception("Invalid config");
    }
}
```

#### Resource Management

```csharp
// ✅ Good - IDisposable pattern
public class ResourceManager : IDisposable
{
    private bool _disposed;
    private readonly HttpClient _httpClient = new();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _httpClient.Dispose();
        GC.SuppressFinalize(this);
    }
}

// ❌ Bad - No disposal
public class ResourceManager
{
    private readonly HttpClient _httpClient = new();
}
```

#### Culture-Invariant Operations

```csharp
// ✅ Good - Culture-invariant comparison
var actionType = parameter.ToLowerInvariant();
if (actionType == "execute") { }

// ❌ Bad - Culture-dependent (fails in Turkish locale)
var actionType = parameter.ToLower();
if (actionType == "execute") { }
```

### XML Documentation

All public APIs must have XML documentation:

```csharp
/// <summary>
/// Executes a workflow asynchronously with retry logic.
/// </summary>
/// <param name="workflowId">Unique identifier for the workflow</param>
/// <param name="cancellationToken">Token to cancel the operation</param>
/// <returns>True if execution succeeded, false otherwise</returns>
/// <exception cref="WorkflowException">Thrown when workflow execution fails</exception>
/// <example>
/// <code>
/// var engine = new WorkflowEngine();
/// bool success = await engine.ExecuteAsync("workflow-123", ct);
/// </code>
/// </example>
public async Task<bool> ExecuteAsync(
    string workflowId,
    CancellationToken cancellationToken = default)
{
    // Implementation
}
```

### Testing Guidelines

#### Test Structure

```csharp
[Fact]
public async Task ExecuteAsync_WithValidWorkflow_ReturnsSuccess()
{
    // Arrange
    var engine = new WorkflowEngine();
    var workflow = CreateTestWorkflow();

    // Act
    var result = await engine.ExecuteAsync(workflow.Id, CancellationToken.None);

    // Assert
    Assert.True(result);
}
```

#### Test Naming

- Use descriptive names: `MethodName_Scenario_ExpectedResult`
- Examples:
  - `ExecuteAsync_WithNullWorkflowId_ThrowsArgumentNullException`
  - `ValidateConfig_WithMissingKey_ThrowsValidationException`
  - `ProcessData_WithCancellation_StopsGracefully`

#### Test Coverage

- **Public APIs**: 100% coverage required
- **Critical paths**: 100% coverage (security, data integrity)
- **Error paths**: Must be tested
- **Edge cases**: Null, empty, boundary values

### Commit Message Guidelines

Follow [Conventional Commits](https://www.conventionalcommits.org/):

```
<type>(<scope>): <subject>

<body>

<footer>
```

**Types:**
- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation changes
- `style`: Formatting changes (no code logic change)
- `refactor`: Code refactoring
- `perf`: Performance improvement
- `test`: Adding or updating tests
- `chore`: Maintenance tasks

**Examples:**

```
feat(cli): add health check command with JSON output

Implement comprehensive health check command that reports:
- System resources (CPU, memory, disk)
- Engine status and metrics
- Component health status

Supports --json flag for machine-readable output.

Closes #123
```

```
fix(security): prevent command injection in process executor

Replace string concatenation with ArgumentList for process execution.
Add command whitelist validation.
Add comprehensive input sanitization.

BREAKING CHANGE: Process execution now requires commands to be
whitelisted in configuration.

Fixes #456
```

## Security Vulnerabilities

**Do NOT** report security vulnerabilities through public GitHub issues.

Instead, please email security details to the maintainers privately. Include:

1. Description of the vulnerability
2. Steps to reproduce
3. Potential impact
4. Suggested mitigation (if any)

We will acknowledge receipt within 48 hours and provide a timeline for resolution.

## Code Review Process

### What to Expect

1. **Initial Review** (1-3 business days): Maintainer will review PR for:
   - Code style compliance
   - Test coverage
   - Documentation completeness
   - Breaking changes assessment

2. **Feedback & Iteration**: Address review comments by:
   - Updating code as suggested
   - Explaining design decisions if needed
   - Adding requested tests or documentation

3. **Approval**: Once approved by maintainers:
   - PR will be merged to `main`
   - Release notes will be updated
   - Credit will be given in CHANGELOG.md

### Review Checklist

- [ ] Code follows style guidelines
- [ ] Tests added for new functionality
- [ ] All tests pass locally
- [ ] Documentation updated
- [ ] No breaking changes (or documented if necessary)
- [ ] Security considerations reviewed
- [ ] Performance impact assessed

## Release Process

Loco follows [Semantic Versioning](https://semver.org/):

- **MAJOR** (1.0.0): Breaking changes
- **MINOR** (0.1.0): New features (backward compatible)
- **PATCH** (0.0.1): Bug fixes (backward compatible)

### Release Checklist

1. Update version in `*.csproj` files
2. Update CHANGELOG.md
3. Update README.md (if needed)
4. Run full test suite
5. Create release tag
6. Build release packages
7. Publish release notes

## Community

### Getting Help

- **GitHub Discussions**: Ask questions, share ideas
- **GitHub Issues**: Report bugs, request features
- **Documentation**: Check docs/ folder first

### Recognition

Contributors will be recognized in:
- CHANGELOG.md for each release
- README.md contributors section
- GitHub contributors graph

## License

By contributing to Loco, you agree that your contributions will be licensed under the MIT License.

---

Thank you for contributing to Loco! 🚀
