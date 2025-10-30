# Loco Testing Guide

## Overview

Loco uses a comprehensive testing strategy with xUnit for unit tests, Moq for mocking, and FluentAssertions for clear test assertions.

**Current Test Coverage:**
- **Core Tests**: 76 tests
- **CLI Tests**: 20 tests
- **Total**: 96 tests (all passing ✅)

## Test Structure

### Core Tests (`tests/Loco.Core.Tests/`)

#### Test Suites:
1. **SimpleLightEnginePersistenceTests** (5 tests)
   - Test persistence layer integration
   - Verify rule storage and retrieval
   - Test data integrity across multiple operations

2. **SimpleLightEngineTests** (19 tests)
   - Core engine lifecycle (start, stop)
   - Rule creation and execution
   - Flow execution
   - Resource management

3. **JsonFileRuleStoreTests** (12 tests)
   - File-based rule persistence
   - CRUD operations (Create, Read, Update, Delete)
   - Data serialization

4. **SimpleSchedulerTests** (15 tests)
   - Scheduled task execution
   - Interval-based scheduling
   - One-time execution scheduling
   - Cancellation logic

5. **LocoConfigTests** (15 tests)
   - Configuration loading from files
   - Environment variable handling
   - Path validation and security

6. **Other Tests** (10 tests)
   - Model validation
   - Utility functions
   - Helper classes

### CLI Tests (`tests/Loco.Cli.Tests/`)

#### Test Suites:
1. **SimpleCommandTests** (4 tests)
   - Basic infrastructure tests
   - Async operation validation

2. **ProgramCommandTests** (16 tests)
   - Command routing and validation
   - All major CLI commands (version, help, start, health, etc.)
   - Command aliases
   - Unknown command handling

## Running Tests

### Run All Tests
```bash
dotnet test
```

### Run Specific Test Project
```bash
# Core tests only
dotnet test tests/Loco.Core.Tests

# CLI tests only
dotnet test tests/Loco.Cli.Tests
```

### Run Specific Test Class
```bash
dotnet test --filter "SimpleLightEnginePersistenceTests"
```

### Run with Verbose Output
```bash
dotnet test -v detailed
```

### Generate Coverage Report
```bash
dotnet test /p:CollectCoverage=true /p:CoverageFormat=json
```

## Test Categories

### Unit Tests (Isolated Functionality)
- Model validation
- Configuration parsing
- Utility functions
- Scheduler logic

### Integration Tests (Component Interaction)
- SimpleLightEngine with rule storage
- Command routing in CLI
- Configuration file loading

### Persistence Tests
- Rule persistence and retrieval
- File storage operations
- Data integrity

## Writing New Tests

### Test File Naming
- `*Tests.cs` suffix for test files
- Example: `SimpleLightEnginePersistenceTests.cs`

### Test Method Naming
Use the pattern: `[Feature]_Should_[ExpectedBehavior]_[Scenario]`

Examples:
```csharp
[Fact]
public async Task SimpleLightEngine_Should_Load_Rules_From_Persistent_Store()

[Fact]
public void JsonFileRuleStore_Should_Handle_Corruption()
```

### Test Structure
Follow the Arrange-Act-Assert (AAA) pattern:

```csharp
[Fact]
public async Task Example_Should_Demonstrate_AAA_Pattern()
{
    // Arrange - Setup test data and dependencies
    var engine = new SimpleLightEngine();
    var rule = CreateTestRule();

    // Act - Execute the behavior being tested
    var result = await engine.ExecuteRuleAsync(rule.Id);

    // Assert - Verify the outcome
    Assert.True(result);
}
```

### Using Assertions
FluentAssertions provides readable assertions:

```csharp
// Good
result.Should().NotBeNull();
status.RuleCount.Should().Be(1);
rules.Should().HaveCount(5);

// Also acceptable
Assert.NotNull(result);
Assert.Equal(1, status.RuleCount);
```

### Using Mocks
Moq for dependency injection:

```csharp
var mockLogger = new Mock<ILogger>();
var engine = new SimpleLightEngine(mockLogger.Object);

// Verify mock was called
mockLogger.Verify(
    l => l.LogInformation(It.IsAny<string>()),
    Times.AtLeastOnce
);
```

## Best Practices

### ✅ Do:
- Keep tests focused and single-purpose
- Use descriptive test names
- Clean up resources (temp files, etc.)
- Use meaningful assertions
- Test both success and error paths
- Keep tests independent (no interdependencies)

### ❌ Don't:
- Test multiple concerns in one test
- Use unclear variable names
- Depend on test execution order
- Ignore cleanup
- Test implementation details instead of behavior
- Skip error case testing

## Continuous Integration

Tests are run automatically on:
- Every commit (local pre-commit hooks recommended)
- Pull requests (GitHub Actions CI/CD)
- Before release builds

## Test Performance

Current test execution times:
- **Core Tests**: ~730ms
- **CLI Tests**: ~95ms
- **Total**: ~825ms (< 1 second)

## Debugging Tests

### Debug with Logging
Add `--logger "console;verbosity=detailed"`:
```bash
dotnet test --logger "console;verbosity=detailed"
```

### Debug in IDE
- Set breakpoints in test method
- Run test with debugger (F5 or Run > Debug)
- IDE will stop at breakpoints

### Filter by Name Pattern
```bash
dotnet test --filter "Persistence"
```

## Coverage Goals

- **Target**: 70%+ code coverage
- **Critical Paths**: 100% coverage for SimpleLightEngine
- **Current Status**: Full coverage for core engine and persistence layer

## Known Limitations

1. **CLI Testing**: Limited to command validation due to entry point architecture
2. **Integration Tests**: Minimal database testing (using in-memory stores)
3. **Performance Tests**: Not yet included

## Future Testing Plans

- [ ] Performance benchmarking suite
- [ ] End-to-end integration tests with database
- [ ] Stress testing for concurrent execution
- [ ] Security testing for input validation
- [ ] Load testing for production scenarios

---

**Last Updated**: October 30, 2024
**Test Framework**: xUnit 2.9.3, Moq 4.20.72, FluentAssertions 8.8.0
