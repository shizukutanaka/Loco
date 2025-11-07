# Loco Visual Editor MVP - Design Document

**Version**: 1.0
**Created**: 2025-11-07
**Status**: 🔄 Design Phase
**Target**: 30-day implementation

---

## 🎯 Executive Summary

Design document for Loco's visual workflow editor MVP - a React-based drag-and-drop interface that enables no-code workflow creation while maintaining full compatibility with the existing JSON-based workflow engine.

### Goals

1. **No-Code Accessibility**: Enable non-developers to create workflows visually
2. **Developer-Friendly**: Maintain JSON export/import for GitOps workflows
3. **Production-Ready**: Full error handling, validation, and testing
4. **Fast to Market**: MVP in 30 days with core features only

### Success Criteria

| Metric | Target | Priority |
|--------|--------|----------|
| **Time to Create Workflow** | <5 minutes (vs <15 min code) | High |
| **User Types Supported** | Developers + No-code users | High |
| **Workflow Compatibility** | 100% with existing engine | Critical |
| **Template Support** | All 10 templates | High |
| **Integration Support** | All 15 integrations | Critical |
| **Performance** | <100ms node operations | Medium |
| **Mobile Responsive** | Not required for MVP | Low |

---

## 📊 Market Analysis

### Competitive Landscape

| Feature | n8n | Zapier | Make | **Loco MVP** |
|---------|-----|--------|------|--------------|
| **Visual Editor** | ✅ Advanced | ✅ Simple | ✅ Advanced | ✅ **Core features** |
| **Drag-and-Drop** | ✅ | ✅ | ✅ | ✅ |
| **Code Export** | JSON | ❌ | ❌ | ✅ **JSON + C#** |
| **GitOps** | Limited | ❌ | ❌ | ✅ **Full support** |
| **Self-Hosted** | ✅ | ❌ | ❌ | ✅ |
| **AI Integration** | Basic | Limited | Limited | ✅ **Native** |
| **Performance** | Good | Good | Good | ✅ **Excellent** |

### Target Users

**Primary**:
- No-code users (50% of target market)
- Business analysts creating workflows
- Product managers prototyping automation

**Secondary**:
- Developers wanting visual overview
- Teams collaborating on workflows
- Enterprise users requiring visual documentation

---

## 🏗️ Architecture Design

### High-Level Architecture

```
┌─────────────────────────────────────────────────────────┐
│                   Loco Visual Editor                     │
│                     (React SPA)                          │
├─────────────────────────────────────────────────────────┤
│                                                          │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐ │
│  │   Toolbar    │  │    Canvas    │  │  Properties  │ │
│  │  Component   │  │  Component   │  │    Panel     │ │
│  └──────────────┘  └──────────────┘  └──────────────┘ │
│                                                          │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐ │
│  │  Node Types  │  │  Connection  │  │  Validation  │ │
│  │   Library    │  │    Engine    │  │    Engine    │ │
│  └──────────────┘  └──────────────┘  └──────────────┘ │
│                                                          │
├─────────────────────────────────────────────────────────┤
│               State Management (Zustand)                 │
├─────────────────────────────────────────────────────────┤
│                                                          │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐ │
│  │ JSON Export  │  │ JSON Import  │  │   REST API   │ │
│  │    Engine    │  │    Parser    │  │    Client    │ │
│  └──────────────┘  └──────────────┘  └──────────────┘ │
│                                                          │
└─────────────────────────────────────────────────────────┘
                           │
                           ▼
                 ┌──────────────────┐
                 │   Loco API       │
                 │  (.NET 8 Backend)│
                 └──────────────────┘
```

### Technology Stack

**Frontend**:
- **Framework**: React 18.x with TypeScript
- **State Management**: Zustand (lightweight, simple)
- **Canvas Library**: React Flow (industry standard for flow diagrams)
- **Styling**: Tailwind CSS (rapid development)
- **HTTP Client**: Axios
- **Validation**: Zod (TypeScript-first schema validation)
- **Build Tool**: Vite (fast, modern)

**Why React Flow?**
- ✅ Industry-standard flow diagram library
- ✅ 20K+ GitHub stars, battle-tested
- ✅ Excellent performance (handles 1000+ nodes)
- ✅ Built-in features: minimap, controls, background
- ✅ TypeScript support
- ✅ Customizable nodes and edges
- ✅ MIT license

**Backend** (Existing):
- .NET 8 Loco API (no changes required)
- JSON workflow definitions (already compatible)

---

## 🎨 User Interface Design

### Layout Structure

```
┌─────────────────────────────────────────────────────────────────┐
│  Loco Visual Editor                        [Save] [Export] [⚙]  │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│ ┌─────────────┐  ┌─────────────────────────────┐  ┌─────────┐ │
│ │             │  │                              │  │         │ │
│ │   Node      │  │         Canvas               │  │ Props   │ │
│ │  Palette    │  │                              │  │ Panel   │ │
│ │             │  │  ┌─────┐                    │  │         │ │
│ │ Triggers    │  │  │Node1│──┐                 │  │ Name:   │ │
│ │  ○ Webhook  │  │  └─────┘  │                 │  │ [____]  │ │
│ │  ○ Schedule │  │            ▼                 │  │         │ │
│ │             │  │         ┌─────┐              │  │ Type:   │ │
│ │ Actions     │  │         │Node2│              │  │ [____]  │ │
│ │  ○ HTTP     │  │         └─────┘              │  │         │ │
│ │  ○ Database │  │                              │  │ Config: │ │
│ │  ○ Email    │  │                              │  │ {...}   │ │
│ │             │  │                              │  │         │ │
│ │ Conditions  │  │                              │  │ [Test]  │ │
│ │  ○ If/Else  │  │                              │  │         │ │
│ │             │  │                              │  │         │ │
│ └─────────────┘  └─────────────────────────────┘  └─────────┘ │
│                                                                  │
│ [Minimap] [Zoom] [Grid: On]                                    │
└─────────────────────────────────────────────────────────────────┘
```

### Component Breakdown

#### 1. Toolbar (Top)
```typescript
interface ToolbarProps {
  workflowName: string;
  onSave: () => void;
  onExport: () => void;
  onImport: () => void;
  onSettings: () => void;
  onUndo: () => void;
  onRedo: () => void;
}
```

Features:
- Workflow name editing
- Save/Load buttons
- Export to JSON
- Import from JSON
- Undo/Redo actions
- Settings menu

#### 2. Node Palette (Left Sidebar)
```typescript
interface NodePaletteProps {
  integrations: Integration[];
  onDragStart: (nodeType: NodeType) => void;
}

interface NodeType {
  category: 'trigger' | 'action' | 'condition' | 'transform' | 'loop';
  integration: string;
  action: string;
  icon: string;
  label: string;
}
```

Categories:
- **Triggers**: Webhook, Schedule, Manual
- **Actions**: HTTP, Database, Email, Slack, GitHub, Discord, etc. (15 integrations)
- **Conditions**: If/Else, Switch
- **Transforms**: Map, Filter, Merge
- **Loops**: ForEach, While

#### 3. Canvas (Center)
```typescript
interface CanvasProps {
  nodes: WorkflowNode[];
  edges: WorkflowEdge[];
  onNodeAdd: (node: WorkflowNode) => void;
  onNodeUpdate: (nodeId: string, data: any) => void;
  onNodeDelete: (nodeId: string) => void;
  onEdgeAdd: (edge: WorkflowEdge) => void;
  onEdgeDelete: (edgeId: string) => void;
}
```

Features:
- Drag-and-drop nodes from palette
- Connect nodes with edges
- Select/move/delete nodes
- Minimap for navigation
- Zoom controls
- Grid background
- Auto-layout

#### 4. Properties Panel (Right Sidebar)
```typescript
interface PropertiesPanelProps {
  selectedNode: WorkflowNode | null;
  onUpdate: (data: any) => void;
  onTest: () => void;
}
```

Features:
- Node configuration form (dynamic based on integration)
- Input validation
- Test connection button
- Variable picker ({{$nodes.xxx}})
- Error display

---

## 🔧 Core Features

### MVP Feature Set

| Feature | Priority | Complexity | Effort |
|---------|----------|------------|--------|
| **Drag-and-drop nodes** | Critical | Medium | 3 days |
| **Connect nodes** | Critical | Low | 1 day |
| **Node configuration** | Critical | High | 5 days |
| **JSON export** | Critical | Low | 1 day |
| **JSON import** | Critical | Medium | 2 days |
| **Validation** | High | Medium | 2 days |
| **Template loading** | High | Low | 1 day |
| **Save/Load workflows** | High | Medium | 2 days |
| **Undo/Redo** | Medium | Medium | 2 days |
| **Minimap** | Low | Low | 0.5 days |
| **Auto-layout** | Low | High | 3 days |

**Total MVP Effort**: ~22-23 days

### Post-MVP Features (Phase 2)

- [ ] Real-time collaboration
- [ ] Workflow versioning
- [ ] Visual debugging
- [ ] Performance profiling
- [ ] Custom node creation
- [ ] Workflow templates marketplace
- [ ] Mobile responsive design

---

## 💾 Data Models

### Workflow State

```typescript
interface WorkflowState {
  id: string;
  name: string;
  description: string;
  nodes: WorkflowNode[];
  edges: WorkflowEdge[];
  viewport: Viewport;
  metadata: WorkflowMetadata;
}

interface WorkflowNode {
  id: string;
  type: 'trigger' | 'action' | 'condition' | 'transform' | 'loop';
  position: { x: number; y: number };
  data: NodeData;
}

interface NodeData {
  label: string;
  integration: string;  // "http", "database", "email", etc.
  action: string;       // "get", "post", "query", "send", etc.
  config: Record<string, any>;
  errors?: string[];
}

interface WorkflowEdge {
  id: string;
  source: string;      // Source node ID
  target: string;      // Target node ID
  sourceHandle?: string;
  targetHandle?: string;
  label?: string;      // "success", "error", etc.
}

interface Viewport {
  x: number;
  y: number;
  zoom: number;
}

interface WorkflowMetadata {
  createdAt: string;
  updatedAt: string;
  createdBy: string;
  version: number;
  tags: string[];
}
```

### JSON Compatibility

The visual editor must produce JSON that is 100% compatible with the existing workflow engine:

```typescript
// Visual Editor Output
{
  "name": "My Workflow",
  "description": "Created with visual editor",
  "nodes": [
    {
      "id": "node_1",
      "name": "Webhook Trigger",
      "type": "trigger",
      "integration": "webhook",
      "action": "receive",
      "config": {
        "path": "/webhooks/my-webhook",
        "method": "POST"
      }
    },
    {
      "id": "node_2",
      "name": "Send to Slack",
      "type": "action",
      "integration": "slack",
      "action": "send",
      "config": {
        "channel": "#alerts",
        "text": "{{$nodes.node_1.body.message}}"
      }
    }
  ],
  "connections": [
    {
      "from": "node_1",
      "to": "node_2"
    }
  ]
}
```

---

## 🔄 User Flows

### Flow 1: Create New Workflow

```
1. User clicks "New Workflow"
   ↓
2. Empty canvas loads with default trigger
   ↓
3. User drags "HTTP Request" from palette to canvas
   ↓
4. User connects trigger to HTTP Request
   ↓
5. User clicks HTTP Request node
   ↓
6. Properties panel shows configuration form
   ↓
7. User fills in URL, method, headers
   ↓
8. User clicks "Test" to validate connection
   ↓
9. Success! User drags "Slack" node
   ↓
10. User connects HTTP Request → Slack
    ↓
11. User configures Slack message
    ↓
12. User clicks "Save"
    ↓
13. Workflow saved to database
```

**Time**: ~3-5 minutes (vs ~10-15 minutes coding)

### Flow 2: Load Template

```
1. User clicks "Templates"
   ↓
2. Modal shows 10 pre-built templates
   ↓
3. User selects "GitHub Issue to Slack"
   ↓
4. Template loads into canvas (pre-configured)
   ↓
5. User modifies Slack channel
   ↓
6. User clicks "Save as New Workflow"
   ↓
7. Workflow saved
```

**Time**: ~1-2 minutes

### Flow 3: Import JSON Workflow

```
1. User clicks "Import"
   ↓
2. File picker opens
   ↓
3. User selects workflow.json
   ↓
4. Parser validates JSON
   ↓
5. Nodes and edges render on canvas
   ↓
6. User can edit visually
```

**Time**: ~30 seconds

---

## 🧪 Validation & Error Handling

### Real-Time Validation

```typescript
interface ValidationRule {
  field: string;
  type: 'required' | 'url' | 'email' | 'json' | 'regex';
  message: string;
}

// Example: HTTP Request validation
const httpValidationRules: ValidationRule[] = [
  { field: 'url', type: 'required', message: 'URL is required' },
  { field: 'url', type: 'url', message: 'Invalid URL format' },
  { field: 'method', type: 'required', message: 'HTTP method is required' }
];
```

### Error States

1. **Missing Connection**: Highlight nodes with no input/output
2. **Invalid Config**: Show red border + error message
3. **Circular Dependencies**: Detect and warn user
4. **Variable Not Found**: Highlight invalid {{}} references

### Visual Indicators

```typescript
enum NodeStatus {
  Valid = 'green',      // All validation passed
  Warning = 'yellow',   // Optional config missing
  Error = 'red',        // Critical error
  Loading = 'blue'      // Test in progress
}
```

---

## 🚀 Implementation Plan

### Week 1: Foundation (Days 1-7)

**Day 1-2**: Project Setup
- [ ] Initialize Vite + React + TypeScript project
- [ ] Install dependencies (React Flow, Zustand, Tailwind, Axios, Zod)
- [ ] Set up directory structure
- [ ] Create base components (Toolbar, Sidebar, Canvas)

**Day 3-4**: Canvas & Nodes
- [ ] Implement React Flow canvas
- [ ] Create custom node components (5 types)
- [ ] Add drag-and-drop from palette
- [ ] Implement node connections

**Day 5-7**: State Management
- [ ] Set up Zustand store
- [ ] Implement workflow state management
- [ ] Add undo/redo functionality
- [ ] Create node selection logic

### Week 2: Core Features (Days 8-14)

**Day 8-10**: Node Configuration
- [ ] Build properties panel
- [ ] Create dynamic forms per integration
- [ ] Implement variable picker ({{}} syntax)
- [ ] Add input validation with Zod

**Day 11-12**: JSON Compatibility
- [ ] Build JSON export engine
- [ ] Build JSON import parser
- [ ] Test with all 10 templates
- [ ] Ensure 100% compatibility

**Day 13-14**: Validation & Testing
- [ ] Implement workflow validation
- [ ] Add error handling
- [ ] Create test suite
- [ ] Fix bugs

### Week 3: Integration & Polish (Days 15-21)

**Day 15-17**: Backend Integration
- [ ] Connect to Loco API
- [ ] Implement save/load workflows
- [ ] Add template loading
- [ ] Test all 15 integrations

**Day 18-20**: UI/UX Polish
- [ ] Add loading states
- [ ] Improve styling
- [ ] Add keyboard shortcuts
- [ ] Optimize performance

**Day 21**: Testing & Documentation
- [ ] End-to-end testing
- [ ] Write user guide
- [ ] Record demo video
- [ ] Prepare for release

### Week 4: Buffer & Launch (Days 22-30)

**Day 22-28**: Bug Fixes & Iteration
- [ ] Fix critical bugs
- [ ] User testing
- [ ] Performance optimization
- [ ] Security audit

**Day 29-30**: Launch
- [ ] Deploy to production
- [ ] Announce launch
- [ ] Monitor for issues
- [ ] Collect feedback

---

## 📊 Performance Requirements

### Target Metrics

| Metric | Target | Critical |
|--------|--------|----------|
| **Initial Load** | <2 seconds | Yes |
| **Node Operation** | <100ms | Yes |
| **JSON Export** | <500ms | No |
| **Canvas Render (100 nodes)** | <1 second | No |
| **Memory Usage** | <50MB | No |
| **Bundle Size** | <500KB (gzipped) | No |

### Optimization Strategies

1. **Code Splitting**: Lazy load integration-specific components
2. **Virtualization**: Only render visible nodes (React Flow built-in)
3. **Debouncing**: Debounce auto-save and validation
4. **Memoization**: Use React.memo for expensive components
5. **Web Workers**: Offload JSON parsing/validation to worker

---

## 🔒 Security Considerations

### Client-Side Security

1. **Input Sanitization**: Sanitize all user inputs
2. **XSS Prevention**: Escape all rendered variables
3. **CSRF Protection**: Use API tokens
4. **Sensitive Data**: Never store secrets in browser (use env vars)

### API Integration

```typescript
// Example: Secure API client
const apiClient = axios.create({
  baseURL: import.meta.env.VITE_API_URL,
  headers: {
    'X-Api-Key': import.meta.env.VITE_API_KEY,
    'Content-Type': 'application/json'
  }
});

// Interceptor for auth token
apiClient.interceptors.request.use((config) => {
  const token = localStorage.getItem('auth_token');
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});
```

---

## 📝 Testing Strategy

### Unit Tests

```typescript
// Example test for node validation
describe('NodeValidation', () => {
  it('should validate HTTP node config', () => {
    const node = {
      type: 'action',
      integration: 'http',
      config: {
        url: 'https://api.example.com',
        method: 'GET'
      }
    };

    const result = validateNode(node);
    expect(result.valid).toBe(true);
  });

  it('should reject invalid URL', () => {
    const node = {
      type: 'action',
      integration: 'http',
      config: {
        url: 'not-a-url',
        method: 'GET'
      }
    };

    const result = validateNode(node);
    expect(result.valid).toBe(false);
    expect(result.errors).toContain('Invalid URL format');
  });
});
```

### Integration Tests

- Test JSON export/import cycle
- Test all 15 integrations
- Test all 10 templates
- Test workflow execution via API

### E2E Tests (Playwright)

```typescript
test('Create workflow via visual editor', async ({ page }) => {
  await page.goto('/editor/new');

  // Drag webhook trigger
  await page.dragAndDrop('[data-node="webhook"]', '.canvas');

  // Drag HTTP action
  await page.dragAndDrop('[data-node="http"]', '.canvas');

  // Connect nodes
  await page.click('[data-handle="webhook-output"]');
  await page.click('[data-handle="http-input"]');

  // Configure HTTP node
  await page.click('[data-node-id="http-1"]');
  await page.fill('[name="url"]', 'https://api.example.com');
  await page.selectOption('[name="method"]', 'GET');

  // Save workflow
  await page.click('[data-testid="save-button"]');

  // Verify saved
  await expect(page.locator('.success-message')).toBeVisible();
});
```

---

## 📱 Deployment Strategy

### Development

```bash
# Local development
npm run dev

# Access at http://localhost:5173
```

### Production Build

```bash
# Build for production
npm run build

# Output: dist/ folder (static files)
```

### Hosting Options

**Option 1: Self-Hosted** (Recommended for MVP)
```nginx
# Nginx configuration
server {
    listen 80;
    server_name editor.loco.dev;

    root /var/www/loco-editor/dist;
    index index.html;

    location / {
        try_files $uri $uri/ /index.html;
    }

    # Proxy API requests to backend
    location /api/ {
        proxy_pass http://localhost:5000/api/;
    }
}
```

**Option 2: Vercel/Netlify** (For quick testing)
- Deploy from GitHub
- Automatic HTTPS
- CDN distribution
- Zero configuration

**Option 3: Docker**
```dockerfile
FROM node:18-alpine as build
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
RUN npm run build

FROM nginx:alpine
COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
```

---

## 📈 Success Metrics

### User Adoption

| Metric | Week 1 | Week 4 | Month 3 |
|--------|--------|--------|---------|
| **Active Users** | 10 | 50 | 200 |
| **Workflows Created** | 20 | 200 | 1,000 |
| **Template Usage** | 50% | 60% | 70% |
| **Avg Time to Create** | 10 min | 7 min | 5 min |

### Technical Metrics

- **Uptime**: >99.5%
- **Error Rate**: <1%
- **Performance**: All targets met
- **User Satisfaction**: >4/5 stars

---

## 🔄 Iteration Plan

### Feedback Collection

1. **In-App Feedback**: Add feedback button
2. **User Interviews**: 5-10 users per week
3. **Analytics**: Track user behavior (Plausible/Umami)
4. **GitHub Issues**: Public feature requests

### Version Roadmap

**v1.0 (MVP)**: Core features (Week 1-4)
**v1.1**: Auto-layout, better UX (Week 5-6)
**v1.2**: Collaboration features (Week 7-10)
**v2.0**: Advanced features + mobile (Month 4-6)

---

## 💰 Cost Analysis

### Development Cost

| Item | Effort | Cost (Assuming $100/hr) |
|------|--------|-------------------------|
| **Frontend Development** | 20 days | $16,000 |
| **Testing** | 3 days | $2,400 |
| **Design/UX** | 2 days | $1,600 |
| **Documentation** | 1 day | $800 |
| **Buffer** | 4 days | $3,200 |
| **Total** | **30 days** | **$24,000** |

### Operational Cost (Monthly)

| Item | Cost |
|------|------|
| **Hosting** (Vercel/Netlify) | $0 (Free tier) |
| **API Backend** (Existing) | $0 |
| **CDN** | $0 (Included) |
| **Total** | **$0/month** |

### ROI Projection

**Cost**: $24,000 (one-time)
**Potential Revenue**:
- 200 users × $49/month (Cloud Pro) = $9,800/month
- Break-even in 2.5 months
- Annual revenue: $117,600

**ROI**: **390% in year 1**

---

## ✅ MVP Acceptance Criteria

### Must Have

- [ ] Drag-and-drop nodes from palette to canvas
- [ ] Connect nodes with visual edges
- [ ] Configure nodes via properties panel
- [ ] Support all 15 integrations
- [ ] Load all 10 templates
- [ ] Export to JSON (100% compatible)
- [ ] Import from JSON (100% compatible)
- [ ] Save/load workflows via API
- [ ] Real-time validation
- [ ] Error handling and display

### Should Have

- [ ] Undo/redo
- [ ] Minimap
- [ ] Zoom controls
- [ ] Keyboard shortcuts
- [ ] Variable picker ({{}} syntax)
- [ ] Test node connections

### Could Have (Post-MVP)

- [ ] Auto-layout algorithm
- [ ] Real-time collaboration
- [ ] Workflow versioning
- [ ] Visual debugging
- [ ] Mobile responsive

---

## 📞 Resources

### Documentation
- [React Flow Docs](https://reactflow.dev/)
- [Zustand Docs](https://zustand-demo.pmnd.rs/)
- [Tailwind CSS Docs](https://tailwindcss.com/)
- [Vite Docs](https://vitejs.dev/)

### Design Inspiration
- n8n Visual Editor
- Zapier Flow Builder
- Make Scenario Builder
- Temporal UI
- Apache Airflow

### Development Team
- Frontend Developer: 1 (full-time)
- Designer: 0.5 (part-time, Week 1-2)
- QA Tester: 0.5 (part-time, Week 3-4)

---

**Document Version**: 1.0
**Created**: 2025-11-07
**Status**: ✅ **Design Complete - Ready for Implementation**
**Next Review**: 2025-11-14 (Week 1 retrospective)

🤖 Generated with [Claude Code](https://claude.com/claude-code)

Co-Authored-By: Claude <noreply@anthropic.com>
