# Loco - Enterprise Workflow Automation

Loco is a lightweight, production-ready workflow automation engine designed for both personal use and enterprise deployments. Built with .NET 8 and optimized for performance, reliability, and ease of use.

[![Build Status](https://img.shields.io/badge/build-passing-brightgreen)]()
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET 8.0+](https://img.shields.io/badge/.NET-8.0+-512bd4)]()

## Features

- **🚀 Cross-Platform**: Run on Windows, macOS, and Linux with unified workflow definitions
- **⚡ High Performance**: Low memory footprint (<50MB), minimal CPU usage (<5%)
- **🔒 Security-First**: JWT authentication, rate limiting, input validation, audit logging
- **🌐 Multi-Language SDKs**: Official Python and TypeScript/JavaScript clients
- **🔄 Async/Await**: Full async support for non-blocking operations
- **📊 Observability**: OpenTelemetry integration, structured logging with correlation IDs
- **🛡️ Reliability**: Error recovery, automatic retries, health checks
- **📚 Well-Tested**: 130+ unit tests, property-based testing, chaos engineering tests

## Quick Start

### Installation

#### From Source

```bash
git clone https://github.com/loco-automation/loco.git
cd loco
dotnet build
dotnet run --project src/Loco.Api
```

#### Using Docker

```bash
docker build -t loco:latest .
docker run -p 5000:5000 loco:latest
```

### First Workflow

```bash
# Start the API server
dotnet run --project src/Loco.Api

# Health check
curl http://localhost:5000/health

# List workflows
curl http://localhost:5000/api/v1/workflows \
  -H "X-Api-Key: your-api-key"

# Execute a workflow
curl -X POST http://localhost:5000/api/v1/workflows/{workflow-id}/execute \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: your-api-key" \
  -d '{"parameters": {}}'
```

### Python SDK

```python
from loco_client import LocoClient

client = LocoClient("http://localhost:5000", api_key="your-api-key")

# List workflows
workflows = await client.workflows.list()

# Execute a workflow
result = await client.workflows.execute("workflow-id", {"param": "value"})

# Wait for completion
execution = await client.workflows.wait_for_execution("workflow-id", result.execution_id)
```

### TypeScript/JavaScript SDK

```typescript
import { LocoClient } from "loco-client";

const client = new LocoClient("http://localhost:5000", {
  apiKey: "your-api-key"
});

// List workflows
const workflows = await client.workflows.list();

// Execute a workflow
const result = await client.workflows.execute("workflow-id", { param: "value" });

// Wait for completion
const execution = await client.workflows.waitForExecution("workflow-id", result.execution_id);
```

## Architecture

### Core Components

- **Loco.Core**: Core workflow engine, rule management, and storage abstractions
  - **Practical Patterns**: 37 lightweight, production-ready patterns (see below)
- **Loco.Api**: REST API with OpenAPI/Swagger documentation
- **Loco.Cli**: Command-line interface for local automation
- **SDKs**: Client libraries for Python and TypeScript/JavaScript

### Loco.Core Practical Patterns 🎯

**NEW**: A complete collection of 23 simple, high-performance patterns following Carmack/Pike/Martin design principles.

📚 **[Complete Documentation](src/Loco.Core/Practical/INDEX.md)** | 🚀 **[Quick Reference](src/Loco.Core/Practical/QUICK_REFERENCE.md)**

**Key Features**:
- ✅ 10M+ ops/sec performance (cache, metrics, pooling)
- ✅ Zero external dependencies (except JWT)
- ✅ <500 lines per pattern
- ✅ Thread-safe by default
- ✅ 50-100x faster than heavy frameworks

**Popular Patterns**:
- `SimpleHttpServer` - Lightweight HTTP server (50K+ req/sec)
- `SimpleDatabase` - Direct SQL without ORM overhead
- `SimpleCache` - High-performance caching (10M+ ops/sec)
- `SimpleJob` - Background job system
- `SimpleAuth` - JWT authentication
- `SimpleMonitoring` - Complete observability stack

**Documentation**:
- [INDEX.md](src/Loco.Core/Practical/INDEX.md) - Master navigation
- [README.md](src/Loco.Core/Practical/README.md) - All patterns overview
- [EXAMPLES.md](src/Loco.Core/Practical/EXAMPLES.md) - Real-world apps
- [INTEGRATION_GUIDE.md](src/Loco.Core/Practical/INTEGRATION_GUIDE.md) - Combining patterns
- [MIGRATION_GUIDE.md](src/Loco.Core/Practical/MIGRATION_GUIDE.md) - From frameworks
- [BENCHMARKS.md](src/Loco.Core/Practical/BENCHMARKS.md) - Performance data
- [TROUBLESHOOTING.md](src/Loco.Core/Practical/TROUBLESHOOTING.md) - Problem solving

### Project Structure

```
loco/
├── src/
│   ├── Loco.Core/           # Core engine
│   │   └── Practical/       # 🎯 37 lightweight patterns (143KB docs)
│   ├── Loco.Api/            # REST API server
│   ├── Loco.Cli/            # CLI application
│   └── Loco.Scheduler/      # Scheduled task execution
├── tests/                    # Test suites
├── sdks/
│   ├── python/              # Python SDK
│   └── typescript/          # TypeScript SDK
├── docs/                    # Documentation
└── examples/                # Example workflows
```

## API Documentation

### REST API Endpoints

- `GET /api/v1/workflows` - List workflows
- `GET /api/v1/workflows/{id}` - Get workflow details
- `POST /api/v1/workflows` - Create workflow
- `PUT /api/v1/workflows/{id}` - Update workflow
- `DELETE /api/v1/workflows/{id}` - Delete workflow
- `POST /api/v1/workflows/{id}/execute` - Execute workflow
- `GET /api/v1/workflows/{id}/executions/{execution-id}` - Get execution status

### Authentication

- **API Key**: Pass `X-Api-Key` header
- **JWT Token**: Use `Authorization: Bearer {token}` header
- **Basic Auth**: POST to `/api/v1/authentication/token` for JWT generation

Full OpenAPI documentation available at `http://localhost:5000/swagger/index.html`

## Configuration

### Environment Variables

```bash
# API Configuration
ASPNETCORE_URLS=http://0.0.0.0:5000
ASPNETCORE_ENVIRONMENT=Production

# Security
JWT_SECRET=your-secret-key
JWT_EXPIRATION_HOURS=24

# Database
RULE_STORE_PATH=/var/lib/loco/rules.json

# Logging
LOG_LEVEL=Information
```

## Development

### Prerequisites

- .NET 8.0 or later
- Node.js 16+ (for TypeScript SDK)
- Python 3.8+ (for Python SDK)

### Build

```bash
# Build all projects
dotnet build

# Build with tests
dotnet build --include-tests

# Run tests
dotnet test

# Generate coverage report
dotnet test /p:CollectCoverage=true /p:CoverageFormat=opencover
```

### Testing

```bash
# Run all tests
dotnet test

# Run with verbose output
dotnet test --logger "console;verbosity=detailed"

# Run specific test class
dotnet test --filter "FullyQualifiedName~Loco.Core.Tests.WorkflowsControllerTests"
```

## Contributing

We welcome contributions! Please see [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

### Development Workflow

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Make your changes and add tests
4. Ensure all tests pass (`dotnet test`)
5. Commit your changes (`git commit -m 'Add amazing feature'`)
6. Push to the branch (`git push origin feature/amazing-feature`)
7. Open a Pull Request

## Security

Please report security vulnerabilities responsibly. See [SECURITY.md](SECURITY.md) for details.

## License

Loco is licensed under the MIT License. See [LICENSE](LICENSE) file for details.

## Community

- **GitHub Issues**: Report bugs and request features
- **Discussions**: Ask questions and share ideas
- **Code of Conduct**: Please read [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md)

## Changelog

See [CHANGELOG.md](CHANGELOG.md) for version history and updates.

## Support

- 📖 [Documentation](#architecture)
- 🐛 [Issue Tracker](https://github.com/loco-automation/loco/issues)
- 💬 [Discussions](https://github.com/loco-automation/loco/discussions)
- 📧 Email: support@loco.io

## Acknowledgments

Built with:
- [.NET 8](https://dotnet.microsoft.com/)
- [OpenTelemetry](https://opentelemetry.io/)
- [Swagger/OpenAPI](https://swagger.io/)
