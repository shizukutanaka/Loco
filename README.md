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
- **🔑 Stored credentials**: connections are encrypted at rest (AES-256-GCM,
  PBKDF2 at 600k iterations) and referenced by ID, so an exported workflow
  never contains a secret. The editor asks for exactly the fields each
  connector declares
- **⏰ Scheduling**: give a trigger node a cron expression and a timezone; the
  scheduler reconciles against the store, so a schedule takes effect within a
  minute of saving without a restart
- **🌐 Client SDKs**: Python and TypeScript/JavaScript clients under `sdks/`
- **🖥️ CLI**: run workflows from the terminal (`Loco.Cli`). `loco workflow
  run-visual <file> --data-dir <path>` executes an editor workflow with the same
  connectors and the same stored connections the API uses

## Project status

This repository is a work in progress. Being honest about the state:

- **Works**: the connector library, the workflow engine, the visual editor
  frontend (builds clean, 432 passing tests), the credential store and its
  UI, the cron scheduler, and the HTTP API's CRUD / execute / validate /
  connections / connectors / auth endpoints.
- **In progress / limitations**: finished executions are persisted to disk and
  survive an API restart, but a run still in flight when the process stopped is
  lost rather than resumed; the CLI's default engine runs a limited action set.
  A workflow can now use two accounts of the same service at once - two Slack
  workspaces, say - which the API used to refuse.
- **CLI caveats**: the command classes under `src/Loco.Cli/Commands/`
  (start, rule, ai, files, logs, health, diag, test, update, setup,
  secrets, backup-config) are now dispatched from `Program.cs` in addition
  to `workflow`/`run`. `loco preset` execution is an explicit simulation
  (it prints the planned actions without performing them). The `loco backup`
  stub was removed rather than implemented: workflows are JSON files in a data
  directory, so backing them up is a directory copy, and `loco backup-config`
  already covers configuration.
- **Dead code removed**: `Loco.Core` went from 89,000 lines to 34,000. Every
  file that remains is reachable from something a user can do - the API's
  controllers and hosted services, the CLI's commands, the tests, or
  reflection-discovered connectors. `scripts/check-structure.py` enforces
  that, along with three other properties that each hid a real defect: a
  consistent package set, tests that only name types which exist, and SDK
  calls that match the API's routes.
- **Backend verification status**: much of the backend was written where
  `dotnet restore` is impossible (api.nuget.org refused by proxy policy), so
  those commits carry a VERIFICATION CAVEAT. The sources have since been
  type-checked offline against the .NET 8 reference assemblies —
  `scripts/typecheck-offline.sh` reports no unexplained errors, meaning every
  remaining compiler error is a type that lives in a NuGet package. That covers
  syntax, signatures, overrides and nullability, but **not** call sites typed by
  packages (ILogger, IHostedService, JwtBearer, …). A full `dotnet build` in
  CI remains the only complete check; see `docs/ci/`. Two things that would
  have stopped that CI run regardless of the network have since been fixed:
  `dotnet restore` failed on NU1008 because two projects pinned versions
  inline, and `Loco.Core.Tests` could not compile because three files named
  types that do not exist.
- **Documentation removed**: 35,000 lines across 33 documents that described a
  codebase which does not exist - PHASE_1 through PHASE_33, an AI framework, a
  governance engine, "quantum-ready autonomy", reports headed "Complete (7 of 7
  systems implemented)" whose every cited file was absent. They were deleted
  with the unreachable code they described. `scripts/check-structure.py` now
  fails when a document cites a source file that is not there.

## Required configuration

Two secrets must be supplied before running the API outside Development. Both
fail fast rather than falling back to something insecure:

| Variable | Why |
|---|---|
| `Jwt:SecretKey` (config) | Signs API tokens. A missing key would otherwise mean a per-run random key, invalidating every token on restart. |
| `LOCO_SECRETS_PASSPHRASE` (env) | Derives the key that encrypts stored connector credentials. Without it, the key is generated into a file **next to** the encrypted data, so anyone who can read the credentials can read the key too. That is acceptable for a single-user CLI on a laptop; it protects nothing on a server. |

In Development both fall back with a loud warning so the app still starts.

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
  -H "Authorization: Bearer $TOKEN"

# Execute a workflow
curl -X POST http://localhost:5000/api/v1/workflows/{workflow-id}/execute \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"input": {}, "dryRun": false}'

# Follow the run. Executions are addressed by their own id, not nested
# under the workflow.
curl http://localhost:5000/api/v1/executions/{execution-id} \
  -H "Authorization: Bearer $TOKEN"
```

### Python SDK

```python
from loco_client import LocoClient

# The client is an async context manager; it authenticates on first use.
async with LocoClient("http://localhost:5000", username="admin", password="…") as client:
    workflows = await client.list_workflows()

    # `input` becomes the run's initial variables, available to every node
    run = await client.execute_workflow("workflow-id", input={"param": "value"})

    # Executions are addressed by their own id
    result = await client.wait_for_execution(run["executionId"])
```

### TypeScript/JavaScript SDK

```typescript
import { LocoClient } from "loco-client";

const client = new LocoClient("http://localhost:5000", {
  username: "admin",
  password: "…"
});

// List workflows
const workflows = await client.workflows.list();

// The second argument is the run's initial variables
const run = await client.workflows.execute("workflow-id", { param: "value" });

// Executions are addressed by their own id
const result = await client.workflows.waitForExecution(run.executionId);
```

## Architecture

### Core Components

- **Loco.Core**: the workflow engine, the connector library, the credential
  store and the cron scheduler
  - **Visual Workflow Engine**: executes a node graph, with retry/backoff,
    error-branch routing and cancellation
  - **28 connectors**, each declaring the credential fields it reads so the
    editor can ask for exactly those:
    - Messaging: Slack, Discord, Teams, Twilio, SendGrid, Email (SMTP)
    - Developer: GitHub, Jira, Linear, HTTP
    - Data: PostgreSQL, MySQL, MongoDB, Redis, Airtable, Google Sheets
    - Storage: AWS S3, Azure Blob Storage
    - Business: Stripe, Shopify, Salesforce, HubSpot, Zendesk, Intercom,
      Notion, Trello, Calendly, Zoom
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
- `POST /api/v1/workflows/{id}/execute` - Start a run (`{"input": {}, "dryRun": false}`)
- `POST /api/v1/workflows/validate` - Validate without saving
- `GET /api/v1/executions/{execution-id}` - Get a run's status, output and logs
- `POST /api/v1/executions/{execution-id}/cancel` - Stop a run
- `GET /api/v1/connectors` - Every connector and the credential fields it declares
- `GET|POST|PUT|DELETE /api/v1/connections` - Stored credentials (write-only secrets)
- `POST /api/v1/connections/{id}/test` - Verify a credential, server-side

### Authentication

- **JWT bearer**: `Authorization: Bearer {token}` on every request
- **Getting a token**: `POST /api/v1/authentication/token` with
  `{"username": ..., "password": ...}`. Users are defined in configuration
  (`Auth:Users`) with PBKDF2-hashed passwords; with none configured the
  endpoint refuses rather than falling back to accept-all.

There is no API-key scheme. The API registers exactly one authentication
handler, JwtBearer, and reads no `X-Api-Key` header - so a request carrying
one is simply unauthenticated.

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
- 📚 [Workflow Documentation](src/Loco.Core/Workflows/README.md) - engine details
- 🚀 [Advanced Scenarios](examples/ADVANCED_SCENARIOS.md) - 4 composite workflows with ROI
- 🔧 [CI setup](docs/ci/) - the consolidated pipeline, and what still blocks it

### Migration Guides
- 🔄 [Coming from Zapier](docs/MIGRATION_GUIDE_ZAPIER.md) - concept comparison (no automated importer yet)
- 🔄 [Coming from n8n](docs/MIGRATION_GUIDE_N8N.md) - concept comparison (no automated importer yet)

### Project Documentation
- 🎨 [Visual Editor Design](docs/VISUAL_EDITOR_DESIGN.md) - MVP architecture and 30-day plan
- 🏆 [Competitive Analysis](docs/COMPETITIVE_ANALYSIS_2025.md) - Market positioning

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
