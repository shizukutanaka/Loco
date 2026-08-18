# Loco - Enterprise Workflow Automation

Loco is a lightweight workflow automation engine built with .NET 8, with a
React/TypeScript visual editor. It is in active development — see
[Project status](#project-status) for what actually works today versus what is
still in progress.

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET 8.0+](https://img.shields.io/badge/.NET-8.0+-512bd4)]()

## Features

- **🔌 Connectors**: 28 built-in connectors (Slack, GitHub, HTTP, databases,
  cloud services, and more) with a clean `IConnector` contract for adding your own
- **🎨 Visual Editor**: React + TypeScript drag-and-drop workflow designer, wired
  to the API for create / save / execute / validate
- **🔒 Authentication**: JWT bearer auth with config-defined users (PBKDF2-hashed
  passwords) and scope-based authorization; rate limiting and input validation
- **📊 Workflow engine**: node-graph execution with retry/backoff, error-branch
  routing, and cancellation
- **🤖 AI steps**: connectors for OpenAI and Anthropic Claude
- **🌐 Client SDKs**: Python and TypeScript/JavaScript clients under `sdks/`
- **🖥️ CLI**: run workflows from the terminal (`Loco.Cli`)

## Project status

This repository is a work in progress. Being honest about the state:

- **Works**: the connector library, the workflow engine, the visual editor
  frontend (builds clean, 196 passing tests), and the HTTP API's CRUD /
  execute / validate / auth endpoints.
- **In progress / limitations**: execution history is in-memory and does not
  survive an API restart; the durable-execution event store is not yet
  file/DB-backed; the CLI's default engine currently runs a limited action set.
- **CLI caveats**: the command classes under `src/Loco.Cli/Commands/`
  (start, rule, ai, files, logs, health, diag, test, update, setup,
  secrets, backup-config) are now dispatched from `Program.cs` in addition
  to `workflow`/`run`. `loco preset` execution is an explicit simulation
  (it prints the planned actions without performing them), and `loco backup`
  remains an honest "not yet implemented" stub.
- **Dead code removed**: the unreachable `Simple*` pattern library, the
  `AIPlatform` "engines", and ten compile-excluded `Loco.Api` subsystems have
  been deleted (~33k lines). Every `<Compile Remove>` is gone from the repo.
- The aspirational `docs/PHASE_9`–`PHASE_14` design documents, which described
  distributed-systems / service-mesh / quantum / zero-knowledge material that was
  never in the codebase, have been **deleted** along with the unreachable code
  they described.

## Quick Start

### Installation

#### From Source

```bash
git clone https://github.com/shizukutanaka/Loco.git
cd loco
dotnet build
dotnet run --project src/Loco.Api
```

#### Using Docker

```bash
docker build -t loco:latest .
docker run -p 5000:5000 loco:latest
```

### Visual Editor (No-Code)

Build workflows visually with drag-and-drop interface:

```bash
# Start the backend API
dotnet run --project src/Loco.Api

# In another terminal, start the Visual Editor
cd src/Loco.VisualEditor
npm install
npm run dev

# Open browser to http://localhost:3000
```

### Code-Based Workflow

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
  - **Visual Workflow Engine**: JSON-based workflow builder with 10 templates - [Docs](src/Loco.Core/Workflows/README.md)
  - **AI Integration**: Multi-provider AI framework (OpenAI, Claude) - [Docs](src/Loco.Core/AI/AIIntegrationFramework.cs)
  - **Pre-built Integrations**: 15 ready-to-use connectors across 3 phases - [Docs](src/Loco.Core/Integrations/README.md)
    - Phase 1: HTTP, Database, Email, Slack, GitHub
    - Phase 2: Discord, Twilio, AWS S3, SendGrid, Telegram
    - Phase 3: Redis, Google Sheets, Stripe, Webhooks, FTP/SFTP
- **Loco.VisualEditor**: React + TypeScript visual workflow builder - [Docs](src/Loco.VisualEditor/README.md)
  - Drag-and-drop canvas with React Flow
  - 5 node types (Trigger, Action, Condition, Transform, Loop)
  - Real-time configuration with dynamic forms
  - JSON export/import for workflow persistence
  - <2s load time, <100ms node operations
- **Loco.Api**: REST API with OpenAPI/Swagger documentation
- **Loco.Cli**: Command-line interface for local automation
- **SDKs**: Client libraries for Python and TypeScript/JavaScript

### Loco.Core/Practical

Formerly a collection of 32 `Simple*` utility classes with 143KB of supporting
documentation. All but `SimpleLogger` had no caller from any compiling entry
point, so they and their docs were deleted. `SimpleLogger` remains because the
AI and connector code genuinely uses it.

### Project Structure

```
loco/
├── src/
│   ├── Loco.Core/           # Core engine
│   │   ├── Workflows/       # 📊 Visual workflow engine (JSON-based, 10 templates)
│   │   ├── AI/              # 🤖 AI integration framework (OpenAI, Claude)
│   │   └── Integrations/    # 🔌 15 pre-built connectors (3 phases)
│   ├── Loco.VisualEditor/   # 🎨 React + TypeScript visual workflow builder
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
dotnet test --filter "FullyQualifiedName~Loco.Api.Tests.WorkflowApiTests"
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

## Documentation

### Getting Started
- 📖 [Getting Started Guide](docs/GETTING_STARTED.md) - 15-minute quick start
- 🏗️ [Architecture](#architecture) - System overview
- 🎨 [Visual Editor Guide](src/Loco.VisualEditor/README.md) - Drag-and-drop workflow builder
- 📚 [Workflow Documentation](src/Loco.Core/Workflows/README.md) - 10 templates and engine details
- 🔌 [Integration Documentation](src/Loco.Core/Integrations/README.md) - 15 production connectors
- 🚀 [Advanced Scenarios](examples/ADVANCED_SCENARIOS.md) - 4 composite workflows with ROI

### Migration Guides
- 🔄 [Coming from Zapier](docs/MIGRATION_GUIDE_ZAPIER.md) - concept comparison (no automated importer yet)
- 🔄 [Coming from n8n](docs/MIGRATION_GUIDE_N8N.md) - concept comparison (no automated importer yet)

### Project Documentation
- 📊 [Project Summary](docs/PROJECT_SUMMARY.md) - Complete implementation overview
- 🎨 [Visual Editor Design](docs/VISUAL_EDITOR_DESIGN.md) - MVP architecture and 30-day plan
- 🏆 [Competitive Analysis](docs/COMPETITIVE_ANALYSIS_2025.md) - Market positioning
- ✅ [Phase 1-5 Completion](IMPLEMENTATION_COMPLETE.md) - Detailed achievements

## Support

- 📖 [Documentation](#documentation) - Complete guides and references
- 🐛 [Issue Tracker](https://github.com/shizukutanaka/Loco/issues)
- 💬 [Discussions](https://github.com/shizukutanaka/Loco/discussions)
- 📧 Email: support@loco.io

## Acknowledgments

Built with:
- [.NET 8](https://dotnet.microsoft.com/)
- [OpenTelemetry](https://opentelemetry.io/)
- [Swagger/OpenAPI](https://swagger.io/)
