# Loco Examples

Welcome to the Loco automation examples! These examples demonstrate core features and common use cases.

## 📚 Available Examples

### 1. [Basic File Automation](01-basic-file-automation.md) ⭐☆☆☆☆
**Difficulty**: Beginner
**Time**: 5 minutes

Learn the basics of Loco automation:
- Creating a SimpleLightEngine instance
- Defining file operations using rules
- Executing rules
- Checking engine status

**What you'll learn**:
- Engine initialization
- Rule creation and execution
- File operations (exists, list, count, size)
- Error handling

---

### 2. [Scheduled Automation](02-scheduled-automation.md) ⭐⭐☆☆☆
**Difficulty**: Intermediate
**Time**: 10 minutes

Automate tasks to run at specific times:
- Periodic rule execution (every X seconds/minutes/hours)
- One-time scheduled execution
- Managing multiple schedules
- Cancelling schedules

**What you'll learn**:
- ScheduleRule for recurring tasks
- ScheduleRuleOnce for one-time execution
- Schedule management
- Long-running services

**Use Cases**:
- Daily backups at 2 AM
- Hourly health checks
- Meeting reminders
- Log rotation

---

### 3. [Rule Persistence](03-rule-persistence.md) ⭐⭐⭐☆☆
**Difficulty**: Intermediate
**Time**: 15 minutes

Persist automation rules across restarts:
- JSON file-based storage
- Automatic rule loading
- CRUD operations on rules
- Data persistence patterns

**What you'll learn**:
- IRuleStore interface
- JsonFileRuleStore implementation
- Rule lifecycle management
- Storage best practices

**Use Cases**:
- Long-running services
- User-defined automation
- Rule templates
- Multi-tenant systems

---

### 4. [Process Execution](04-process-execution.md) ⭐⭐☆☆☆
**Difficulty**: Intermediate
**Time**: 5 minutes

Execute external processes and commands:
- Running system commands
- Executing scripts (PowerShell, batch)
- Passing arguments
- Capturing output

**Use Cases**:
- System maintenance
- Build automation
- Deployment scripts
- External tool integration

---

### 5. [Configuration & Validation](05-configuration.md) ⭐⭐☆☆☆
**Difficulty**: Intermediate
**Time**: 10 minutes

Configure and validate the automation engine:
- Custom configuration options
- Configuration validation
- Handling validation errors/warnings
- Best practices for config management

**What you'll learn**:
- LocoConfig options
- ConfigValidator usage
- Error handling
- Production configuration

**Configuration Options**:
- MaxConcurrentFlows
- DefaultTimeoutSeconds
- DefaultRetryCount
- LogLevel
- EnableFileLogging
- And more...

---

## 🚀 Getting Started

### Prerequisites
- .NET 8.0 SDK installed
- Loco.Core library (included in this repository)
- Basic C# knowledge

### Quick Start

1. **Clone the repository**:
   ```bash
   git clone https://github.com/yourusername/Loco.git
   cd Loco
   ```

2. **Build the project**:
   ```bash
   dotnet build --configuration Release
   ```

3. **Create a new console app for testing**:
   ```bash
   dotnet new console -n LocoExample
   cd LocoExample
   dotnet add reference ../src/Loco.Core/Loco.Core.csproj
   ```

4. **Copy an example to Program.cs** and run:
   ```bash
   dotnet run
   ```

---

## 📖 Learning Path

We recommend following the examples in order:

1. **Start with Example 1** (Basic File Automation)
   - Understand core concepts
   - Get familiar with the API
   - See a complete working example

2. **Move to Example 2** (Scheduled Automation)
   - Learn about scheduling
   - Understand long-running services
   - Explore time-based automation

3. **Study Example 3** (Rule Persistence)
   - Master data persistence
   - Understand the IRuleStore pattern
   - Learn CRUD operations

4. **Experiment with Example 4** (Process Execution)
   - Integrate external tools
   - Run system commands
   - Capture and process output

5. **Finish with Example 5** (Configuration)
   - Configure for production
   - Validate settings
   - Handle errors properly

---

## 🎯 Common Use Cases

### Automated Backups
Combine Examples 1, 2, and 3:
- File operations (Example 1)
- Daily scheduling (Example 2)
- Persistent rules (Example 3)

### System Monitoring
Combine Examples 2 and 4:
- Scheduled checks (Example 2)
- Execute monitoring tools (Example 4)
- Log results

### CI/CD Integration
Combine Examples 4 and 5:
- Run build scripts (Example 4)
- Custom configuration (Example 5)
- Error handling

### Data Processing Pipelines
Combine Examples 1, 2, and 3:
- File discovery and processing (Example 1)
- Scheduled execution (Example 2)
- Persistent workflows (Example 3)

---

## 🔧 Example Structure

Each example includes:

- **Overview**: What the example demonstrates
- **Code Example**: Complete, runnable C# code
- **Step-by-Step Explanation**: Detailed code walkthrough
- **Advanced Usage**: Additional patterns and techniques
- **Running the Example**: Build and execution instructions
- **Expected Output**: What you should see
- **Use Cases**: Real-world applications
- **Common Issues**: Troubleshooting guide
- **Next Steps**: Related examples and documentation

---

## 💡 Tips for Success

### 1. Read the Code Comments
All examples include detailed comments explaining each step.

### 2. Experiment
Modify the examples to fit your use case. Change parameters, add actions, combine examples.

### 3. Check the API Documentation
Refer to [docs/API.md](../docs/API.md) for complete API reference.

### 4. Use the CLI
The Loco CLI can help you test and debug:
```bash
./src/Loco.Cli/bin/Release/net8.0/Loco.Cli.exe version
./src/Loco.Cli/bin/Release/net8.0/Loco.Cli.exe health
```

### 5. Enable Logging
Add console logging to see what's happening:
```csharp
using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Debug);
});
var logger = loggerFactory.CreateLogger<SimpleLightEngine>();
using var engine = new SimpleLightEngine(logger);
```

---

## 🐛 Troubleshooting

### Build Errors
**Error**: "Project file does not exist"
**Solution**: Ensure you're in the correct directory and paths are correct

### Runtime Errors
**Error**: "FileNotFoundException"
**Solution**: Check that source files exist before running

### Permission Errors
**Error**: "Access to the path is denied"
**Solution**: Use user-accessible directories or run with appropriate permissions

### More Help
- Check [TROUBLESHOOTING.md](../TROUBLESHOOTING.md)
- Review [FAQ.md](../FAQ.md)
- See [docs/DEVELOPER.md](../docs/DEVELOPER.md)

---

## 📚 Additional Resources

### Documentation
- [API Reference](../docs/API.md)
- [User Manual](../docs/USER_MANUAL.md)
- [Developer Guide](../docs/DEVELOPER.md)

### Project Information
- [README](../README.md)
- [CHANGELOG](../CHANGELOG.md)
- [CONTRIBUTING](../CONTRIBUTING.md)

---

## 🤝 Contributing Examples

Have a great use case? Submit a new example!

1. Fork the repository
2. Create your example in `examples/XX-your-example.md`
3. Follow the existing example structure
4. Submit a pull request

See [CONTRIBUTING.md](../CONTRIBUTING.md) for details.

---

## 📊 Example Comparison

| Example | Difficulty | Time | Key Concepts |
|---------|-----------|------|--------------|
| 01 - Basic File Automation | ⭐☆☆☆☆ | 5 min | Engine, Rules, File Actions |
| 02 - Scheduled Automation | ⭐⭐☆☆☆ | 10 min | Scheduling, Timing |
| 03 - Rule Persistence | ⭐⭐⭐☆☆ | 15 min | Storage, CRUD, Lifecycle |
| 04 - Process Execution | ⭐⭐☆☆☆ | 5 min | External Tools, Commands |
| 05 - Configuration | ⭐⭐☆☆☆ | 10 min | Config, Validation |

---

**Happy Automating!** 🚀

If you have questions, check the [FAQ](../FAQ.md) or [User Manual](../docs/USER_MANUAL.md).

---

*Last Updated*: 2025-01-16
*Version*: 0.1.0-alpha
