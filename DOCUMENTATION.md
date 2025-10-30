# Loco Documentation Guide

## 📚 Documentation Structure

Loco uses a three-level documentation hierarchy for maximum clarity:

### Level 1: Root Directory (Project Overview)
- **[README.md](README.md)** - Project overview, quick start, and features
- **[SPECIFICATION.md](SPECIFICATION.md)** - Complete technical specification
- **[CHANGELOG.md](CHANGELOG.md)** - Version history and release notes
- **[DOCUMENTATION.md](DOCUMENTATION.md)** - This file - documentation navigation

### Level 2: `/docs` Directory (Detailed Documentation)
See [docs/README.md](docs/README.md) for complete documentation index

Key documents:
- **[docs/QUICKSTART.md](docs/QUICKSTART.md)** - 5-minute setup guide
- **[docs/USER_MANUAL.md](docs/USER_MANUAL.md)** - User guide
- **[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)** - System architecture
- **[docs/API_REFERENCE.md](docs/API_REFERENCE.md)** - API documentation
- **[docs/DEPLOYMENT.md](docs/DEPLOYMENT.md)** - Deployment guide
- **[docs/EXTENSION_DEVELOPMENT.md](docs/EXTENSION_DEVELOPMENT.md)** - Extension development

### Level 3: `/examples` Directory (Code Examples)
Located in `examples/` directory with sample code and workflows

## 🎯 Quick Navigation by Audience

### I want to...
- **Get started quickly** → [docs/QUICKSTART.md](docs/QUICKSTART.md)
- **Learn how to use Loco** → [docs/USER_MANUAL.md](docs/USER_MANUAL.md)
- **Understand the architecture** → [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)
- **Build a custom extension** → [docs/EXTENSION_DEVELOPMENT.md](docs/EXTENSION_DEVELOPMENT.md)
- **Integrate with my system** → [docs/API_REFERENCE.md](docs/API_REFERENCE.md)
- **Deploy to production** → [docs/DEPLOYMENT.md](docs/DEPLOYMENT.md)
- **See the complete roadmap** → [docs/IMPLEMENTATION_ROADMAP.md](docs/IMPLEMENTATION_ROADMAP.md)
- **Check what changed** → [CHANGELOG.md](CHANGELOG.md)

## 📋 Document Map

```
Loco/
├── README.md                          # Project overview
├── CHANGELOG.md                       # Version history
├── SPECIFICATION.md                   # Technical specification
├── DOCUMENTATION.md                   # This file
├── docs/
│   ├── README.md                      # Documentation index
│   ├── QUICKSTART.md                  # 5-minute guide
│   ├── USER_MANUAL.md                 # User guide
│   ├── ARCHITECTURE.md                # System design
│   ├── API_REFERENCE.md               # API docs
│   ├── EXTENSION_DEVELOPMENT.md       # Extension guide
│   ├── DEPLOYMENT.md                  # Deployment guide
│   ├── IMPLEMENTATION_ROADMAP.md      # Feature roadmap
│   ├── AUTOMATION_ANALYSIS.md         # Use case analysis
│   ├── COMPREHENSIVE_AUTOMATION_ANALYSIS.md # Detailed scenarios
│   ├── MULTILINGUAL_RESEARCH_SUMMARY.md    # Internationalization
│   └── 2025_GLOBAL_TRANSFORMATION.md      # Strategic vision
├── examples/                          # Code examples and workflows
│   ├── extensions/                    # Extension examples
│   ├── workflows/                     # Workflow examples
│   └── *.cs                           # Code samples
└── src/
    ├── Loco.Cli/                      # CLI implementation
    ├── Loco.Core/                     # Core engine
    └── Loco.Api/                      # API layer
```

## 🔗 Related Links

- **Project Repository**: GitHub repository link
- **Issue Tracker**: Report bugs and request features
- **Contributing**: See CONTRIBUTING.md
- **License**: See LICENSE file
- **Code of Conduct**: See CODE_OF_CONDUCT.md

## 📖 How to Read the Documentation

1. **New to Loco?** Start with [README.md](README.md) then [docs/QUICKSTART.md](docs/QUICKSTART.md)
2. **Want to understand how it works?** Read [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)
3. **Need API details?** Check [docs/API_REFERENCE.md](docs/API_REFERENCE.md)
4. **Ready to deploy?** Follow [docs/DEPLOYMENT.md](docs/DEPLOYMENT.md)
5. **Planning to extend?** See [docs/EXTENSION_DEVELOPMENT.md](docs/EXTENSION_DEVELOPMENT.md)

## 📝 Documentation Standards

All documentation in this project follows these standards:

- **Clarity**: Technical accuracy with clear explanations
- **Examples**: Practical code examples where applicable
- **Bilingual**: English and Japanese support for key documents
- **Accessibility**: Proper formatting and table of contents
- **Versioning**: Aligned with CHANGELOG.md for version tracking

## 🔄 Keeping Documentation Updated

Documentation is maintained alongside code. When making changes:

1. Update relevant documentation files
2. Reference the GitHub issue or PR number
3. Add entry to CHANGELOG.md if user-facing
4. Ensure examples still work after changes

---

**Last Updated**: October 30, 2024
**Documentation Version**: Aligned with Loco v0.2.0-alpha
