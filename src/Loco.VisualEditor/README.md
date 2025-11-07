# Loco Visual Editor

Visual workflow editor for the Loco automation platform. Built with React, TypeScript, and React Flow.

## Features

- **Visual Canvas**: Drag-and-drop workflow builder with React Flow
- **15 Integrations**: Pre-built connectors for HTTP, Database, Slack, GitHub, and more
- **Node Types**: Trigger, Action, Condition, Transform, and Loop nodes
- **Real-time Configuration**: Property panel for configuring node parameters
- **JSON Export/Import**: Save and load workflows as JSON
- **Performance**: Optimized for workflows with 100+ nodes

## Tech Stack

- **React 18** - UI framework
- **TypeScript** - Type safety
- **Vite** - Build tool
- **React Flow** - Visual canvas
- **Zustand** - State management
- **Tailwind CSS** - Styling
- **Lucide React** - Icons

## Getting Started

### Prerequisites

- Node.js 18+ and npm

### Installation

```bash
# Install dependencies
npm install

# Start development server
npm run dev

# Build for production
npm run build

# Preview production build
npm run preview
```

## Project Structure

```
src/
├── components/
│   ├── Canvas/          # React Flow canvas
│   ├── NodePalette/     # Draggable node list
│   ├── NodeTypes/       # Custom node components
│   ├── PropertyPanel/   # Node configuration panel
│   └── Toolbar/         # Workflow operations
├── data/
│   └── integrations.ts  # Integration definitions
├── store/
│   └── workflowStore.ts # Zustand state management
├── types/
│   └── workflow.ts      # TypeScript types
├── styles/
│   └── index.css        # Global styles
└── App.tsx              # Main application
```

## Development

The Visual Editor communicates with the Loco backend API at `http://localhost:5000/api`.

### Available Scripts

- `npm run dev` - Start development server (port 3000)
- `npm run build` - Build for production
- `npm run preview` - Preview production build
- `npm run lint` - Run ESLint

## Integration with Backend

The Visual Editor exports workflows in the Loco JSON format:

```json
{
  "id": "workflow-id",
  "name": "My Workflow",
  "nodes": [...],
  "edges": [...],
  "metadata": {...}
}
```

These workflows can be executed by the Loco backend engine.

## License

MIT
