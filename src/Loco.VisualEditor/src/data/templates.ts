import { Workflow } from '@/types/workflow';

export interface WorkflowTemplate {
  id: string;
  name: string;
  description: string;
  category: 'communication' | 'automation' | 'data' | 'ai' | 'monitoring';
  icon: string;
  workflow: Workflow;
}

export const templates: WorkflowTemplate[] = [
  // 1. Slack Notification
  {
    id: 'slack-notification',
    name: 'Slack Notification',
    description: 'Send a message to Slack when webhook is triggered',
    category: 'communication',
    icon: '💬',
    workflow: {
      id: crypto.randomUUID(),
      name: 'Slack Notification',
      description: 'Webhook to Slack message',
      nodes: [
        {
          id: 'trigger-1',
          type: 'trigger',
          position: { x: 100, y: 100 },
          data: {
            label: 'HTTP Webhook',
            integration: 'http',
            config: { path: '/webhook' },
          },
        },
        {
          id: 'action-1',
          type: 'action',
          position: { x: 400, y: 100 },
          data: {
            label: 'Send to Slack',
            integration: 'slack',
            config: {
              action: 'sendMessage',
              parameters: {
                channel: '#general',
                text: 'New webhook received',
              },
            },
          },
        },
      ],
      edges: [
        {
          id: 'edge-1',
          source: 'trigger-1',
          target: 'action-1',
        },
      ],
      metadata: { version: '1.0', isPublic: true },
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
    },
  },

  // 2. Database to Email
  {
    id: 'database-to-email',
    name: 'Database Query & Email',
    description: 'Query database and email results',
    category: 'data',
    icon: '📧',
    workflow: {
      id: crypto.randomUUID(),
      name: 'Database to Email',
      description: 'Query data and send via email',
      nodes: [
        {
          id: 'trigger-1',
          type: 'trigger',
          position: { x: 100, y: 100 },
          data: {
            label: 'HTTP Webhook',
            integration: 'http',
            config: {},
          },
        },
        {
          id: 'action-1',
          type: 'action',
          position: { x: 400, y: 100 },
          data: {
            label: 'Query Database',
            // 'database' is not a connector; PostgreSqlConnector is, and its
            // query action reads 'sql' (not 'query').
            integration: 'postgresql',
            config: {
              action: 'query',
              parameters: {
                sql: 'SELECT * FROM users WHERE active = true',
              },
            },
          },
        },
        {
          id: 'action-2',
          type: 'action',
          position: { x: 700, y: 100 },
          data: {
            label: 'Send Email',
            integration: 'email',
            config: {
              action: 'send',
              parameters: {
                to: 'admin@example.com',
                subject: 'Database Report',
                body: 'See attached results',
              },
            },
          },
        },
      ],
      edges: [
        {
          id: 'edge-1',
          source: 'trigger-1',
          target: 'action-1',
        },
        {
          id: 'edge-2',
          source: 'action-1',
          target: 'action-2',
        },
      ],
      metadata: { version: '1.0', isPublic: true },
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
    },
  },

  // 3. Conditional Workflow
  {
    id: 'conditional-workflow',
    name: 'Conditional Routing',
    description: 'Route based on data condition',
    category: 'automation',
    icon: '🔀',
    workflow: {
      id: crypto.randomUUID(),
      name: 'Conditional Routing',
      description: 'Branch workflow based on conditions',
      nodes: [
        {
          id: 'trigger-1',
          type: 'trigger',
          position: { x: 100, y: 150 },
          data: {
            label: 'HTTP Webhook',
            integration: 'http',
            config: {},
          },
        },
        {
          id: 'condition-1',
          type: 'condition',
          position: { x: 400, y: 150 },
          data: {
            label: 'Check Amount',
            config: { condition: 'item.amount > 100' },
          },
        },
        {
          id: 'action-1',
          type: 'action',
          position: { x: 700, y: 50 },
          data: {
            label: 'High Value Alert',
            integration: 'slack',
            config: {
              action: 'sendMessage',
              parameters: {
                channel: '#alerts',
                text: 'High value transaction',
              },
            },
          },
        },
        {
          id: 'action-2',
          type: 'action',
          position: { x: 700, y: 250 },
          data: {
            label: 'Normal Processing',
            integration: 'postgresql',
            config: {
              action: 'query',
              parameters: {},
            },
          },
        },
      ],
      edges: [
        {
          id: 'edge-1',
          source: 'trigger-1',
          target: 'condition-1',
        },
        {
          id: 'edge-2',
          source: 'condition-1',
          target: 'action-1',
          sourceHandle: 'true',
        },
        {
          id: 'edge-3',
          source: 'condition-1',
          target: 'action-2',
          sourceHandle: 'false',
        },
      ],
      metadata: { version: '1.0', isPublic: true },
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
    },
  },

  // 4. Data Transform Pipeline
  {
    id: 'data-transform',
    name: 'Data Transform Pipeline',
    description: 'Transform and enrich data',
    category: 'data',
    icon: '🔄',
    workflow: {
      id: crypto.randomUUID(),
      name: 'Data Transform Pipeline',
      description: 'Extract, transform, and load data',
      nodes: [
        {
          id: 'trigger-1',
          type: 'trigger',
          position: { x: 100, y: 100 },
          data: {
            label: 'HTTP Webhook',
            integration: 'http',
            config: {},
          },
        },
        {
          id: 'transform-1',
          type: 'transform',
          position: { x: 400, y: 100 },
          data: {
            label: 'Transform Data',
            integration: 'transform',
            config: {
              code: 'return items.Select(item => new { Name = item.name.ToUpper(), Total = item.price * item.quantity }).ToList();',
            },
          },
        },
        {
          id: 'action-1',
          type: 'action',
          position: { x: 700, y: 100 },
          data: {
            label: 'Save to Database',
            integration: 'postgresql',
            config: {
              action: 'query',
              parameters: {},
            },
          },
        },
      ],
      edges: [
        {
          id: 'edge-1',
          source: 'trigger-1',
          target: 'transform-1',
        },
        {
          id: 'edge-2',
          source: 'transform-1',
          target: 'action-1',
        },
      ],
      metadata: { version: '1.0', isPublic: true },
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
    },
  },

  // 5. Multi-Channel Notification
  {
    id: 'multi-channel',
    name: 'Multi-Channel Notification',
    description: 'Send notifications to multiple channels',
    category: 'communication',
    icon: '📢',
    workflow: {
      id: crypto.randomUUID(),
      name: 'Multi-Channel Notification',
      description: 'Notify via Slack, Email, and Discord',
      nodes: [
        {
          id: 'trigger-1',
          type: 'trigger',
          position: { x: 100, y: 150 },
          data: {
            label: 'HTTP Webhook',
            integration: 'http',
            config: {},
          },
        },
        {
          id: 'action-1',
          type: 'action',
          position: { x: 400, y: 50 },
          data: {
            label: 'Slack',
            integration: 'slack',
            config: { action: 'sendMessage' },
          },
        },
        {
          id: 'action-2',
          type: 'action',
          position: { x: 400, y: 150 },
          data: {
            label: 'Email',
            integration: 'email',
            config: { action: 'send' },
          },
        },
        {
          id: 'action-3',
          type: 'action',
          position: { x: 400, y: 250 },
          data: {
            label: 'Discord',
            integration: 'discord',
            config: { action: 'sendMessage' },
          },
        },
      ],
      edges: [
        {
          id: 'edge-1',
          source: 'trigger-1',
          target: 'action-1',
        },
        {
          id: 'edge-2',
          source: 'trigger-1',
          target: 'action-2',
        },
        {
          id: 'edge-3',
          source: 'trigger-1',
          target: 'action-3',
        },
      ],
      metadata: { version: '1.0', isPublic: true },
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
    },
  },

  // 6. GitHub Issue Tracker
  {
    id: 'github-issue',
    name: 'GitHub Issue Tracker',
    description: 'Create GitHub issues from webhooks',
    category: 'automation',
    icon: '🐙',
    workflow: {
      id: crypto.randomUUID(),
      name: 'GitHub Issue Tracker',
      description: 'Auto-create GitHub issues',
      nodes: [
        {
          id: 'trigger-1',
          type: 'trigger',
          position: { x: 100, y: 100 },
          data: {
            label: 'HTTP Webhook',
            integration: 'http',
            config: {},
          },
        },
        {
          id: 'action-1',
          type: 'action',
          position: { x: 400, y: 100 },
          data: {
            label: 'Create Issue',
            integration: 'github',
            config: {
              action: 'createIssue',
              parameters: {
                owner: 'myorg',
                repo: 'myrepo',
                title: 'New Bug Report',
              },
            },
          },
        },
        {
          id: 'action-2',
          type: 'action',
          position: { x: 700, y: 100 },
          data: {
            label: 'Notify Slack',
            integration: 'slack',
            config: { action: 'sendMessage' },
          },
        },
      ],
      edges: [
        {
          id: 'edge-1',
          source: 'trigger-1',
          target: 'action-1',
        },
        {
          id: 'edge-2',
          source: 'action-1',
          target: 'action-2',
        },
      ],
      metadata: { version: '1.0', isPublic: true },
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
    },
  },

  // 7. File Upload to S3
  {
    id: 's3-upload',
    name: 'S3 File Upload',
    description: 'Upload files to AWS S3 with notification',
    category: 'data',
    icon: '☁️',
    workflow: {
      id: crypto.randomUUID(),
      name: 'S3 File Upload',
      description: 'Upload to S3 and notify',
      nodes: [
        {
          id: 'trigger-1',
          type: 'trigger',
          position: { x: 100, y: 100 },
          data: {
            label: 'HTTP Webhook',
            integration: 'http',
            config: {},
          },
        },
        {
          id: 'action-1',
          type: 'action',
          position: { x: 400, y: 100 },
          data: {
            label: 'Upload to S3',
            integration: 's3',
            config: {
              action: 'upload',
              parameters: {
                bucket: 'my-bucket',
                key: 'uploads/file.txt',
              },
            },
          },
        },
        {
          id: 'action-2',
          type: 'action',
          position: { x: 700, y: 100 },
          data: {
            label: 'Send Email',
            integration: 'email',
            config: { action: 'send' },
          },
        },
      ],
      edges: [
        {
          id: 'edge-1',
          source: 'trigger-1',
          target: 'action-1',
        },
        {
          id: 'edge-2',
          source: 'action-1',
          target: 'action-2',
        },
      ],
      metadata: { version: '1.0', isPublic: true },
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
    },
  },

  // 8. Redis Cache Update
  {
    id: 'redis-cache',
    name: 'Redis Cache Update',
    description: 'Update Redis cache from database',
    category: 'data',
    icon: '⚡',
    workflow: {
      id: crypto.randomUUID(),
      name: 'Redis Cache Update',
      description: 'Sync database to cache',
      nodes: [
        {
          id: 'trigger-1',
          type: 'trigger',
          position: { x: 100, y: 100 },
          data: {
            label: 'HTTP Webhook',
            integration: 'http',
            config: {},
          },
        },
        {
          id: 'action-1',
          type: 'action',
          position: { x: 400, y: 100 },
          data: {
            label: 'Query Database',
            integration: 'postgresql',
            config: { action: 'query' },
          },
        },
        {
          id: 'action-2',
          type: 'action',
          position: { x: 700, y: 100 },
          data: {
            label: 'Update Redis',
            integration: 'redis',
            config: {
              action: 'set',
              parameters: {
                key: 'cache:users',
                ttl: 3600,
              },
            },
          },
        },
      ],
      edges: [
        {
          id: 'edge-1',
          source: 'trigger-1',
          target: 'action-1',
        },
        {
          id: 'edge-2',
          source: 'action-1',
          target: 'action-2',
        },
      ],
      metadata: { version: '1.0', isPublic: true },
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
    },
  },

  // 9. Loop Processing
  {
    id: 'loop-processing',
    name: 'Loop Processing',
    description: 'Process items in a loop',
    category: 'automation',
    icon: '🔁',
    workflow: {
      id: crypto.randomUUID(),
      name: 'Loop Processing',
      description: 'Iterate and process each item',
      nodes: [
        {
          id: 'trigger-1',
          type: 'trigger',
          position: { x: 100, y: 150 },
          data: {
            label: 'HTTP Webhook',
            integration: 'http',
            config: {},
          },
        },
        {
          id: 'loop-1',
          type: 'loop',
          position: { x: 400, y: 150 },
          data: {
            label: 'For Each Item',
            config: {},
          },
        },
        {
          id: 'action-1',
          type: 'action',
          position: { x: 700, y: 150 },
          data: {
            label: 'Process Item',
            integration: 'postgresql',
            config: { action: 'query' },
          },
        },
      ],
      edges: [
        {
          id: 'edge-1',
          source: 'trigger-1',
          target: 'loop-1',
        },
        {
          id: 'edge-2',
          source: 'loop-1',
          target: 'action-1',
        },
      ],
      metadata: { version: '1.0', isPublic: true },
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
    },
  },

  // 10. Stripe Payment Processing
  {
    id: 'stripe-payment',
    name: 'Stripe Payment Processing',
    description: 'Process payments and send receipts',
    category: 'automation',
    icon: '💳',
    workflow: {
      id: crypto.randomUUID(),
      name: 'Stripe Payment Processing',
      description: 'Handle payments end-to-end',
      nodes: [
        {
          id: 'trigger-1',
          type: 'trigger',
          position: { x: 100, y: 150 },
          data: {
            label: 'HTTP Webhook',
            integration: 'http',
            config: {},
          },
        },
        {
          id: 'action-1',
          type: 'action',
          position: { x: 400, y: 150 },
          data: {
            label: 'Create Charge',
            integration: 'stripe',
            config: {
              action: 'createCharge',
              parameters: {
                currency: 'usd',
              },
            },
          },
        },
        {
          id: 'condition-1',
          type: 'condition',
          position: { x: 700, y: 150 },
          data: {
            label: 'Check Success',
            config: { condition: 'payment.status == "succeeded"' },
          },
        },
        {
          id: 'action-2',
          type: 'action',
          position: { x: 1000, y: 50 },
          data: {
            label: 'Send Receipt',
            integration: 'sendgrid',
            // SendGridConnector's action id is 'sendEmail'; 'send' resolved to nothing.
            config: { action: 'sendEmail' },
          },
        },
        {
          id: 'action-3',
          type: 'action',
          position: { x: 1000, y: 250 },
          data: {
            label: 'Send Error Email',
            integration: 'email',
            config: { action: 'send' },
          },
        },
      ],
      edges: [
        {
          id: 'edge-1',
          source: 'trigger-1',
          target: 'action-1',
        },
        {
          id: 'edge-2',
          source: 'action-1',
          target: 'condition-1',
        },
        {
          id: 'edge-3',
          source: 'condition-1',
          target: 'action-2',
          sourceHandle: 'true',
        },
        {
          id: 'edge-4',
          source: 'condition-1',
          target: 'action-3',
          sourceHandle: 'false',
        },
      ],
      metadata: { version: '1.0', isPublic: true },
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
    },
  },
];

export function getTemplateById(id: string): WorkflowTemplate | undefined {
  return templates.find((t) => t.id === id);
}

export function getTemplatesByCategory(
  category: WorkflowTemplate['category']
): WorkflowTemplate[] {
  return templates.filter((t) => t.category === category);
}
