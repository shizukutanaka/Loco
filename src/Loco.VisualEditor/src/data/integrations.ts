import { Integration } from '@/types/workflow';

export const integrations: Integration[] = [
  // Phase 1 - Core
  {
    id: 'http',
    name: 'HTTP Request',
    category: 'web',
    icon: '🌐',
    description: 'Make HTTP requests to any API endpoint',
    // HttpConnector models one action PER METHOD (get/post/put/patch/delete),
    // not a single "request" action with a method parameter. The palette must
    // match, because the engine resolves handlers by `${integration}:${action}`.
    actions: [
      {
        id: 'get',
        name: 'GET Request',
        description: 'Send HTTP GET request',
        parameters: [
          { name: 'url', type: 'string', required: true, description: 'Request URL' },
          { name: 'headers', type: 'json', required: false, description: 'Request headers' },
        ],
      },
      {
        id: 'post',
        name: 'POST Request',
        description: 'Send HTTP POST request with body',
        parameters: [
          { name: 'url', type: 'string', required: true, description: 'Request URL' },
          { name: 'headers', type: 'json', required: false, description: 'Request headers' },
          { name: 'body', type: 'json', required: false, description: 'Request body' },
        ],
      },
      {
        id: 'put',
        name: 'PUT Request',
        description: 'Send HTTP PUT request with body',
        parameters: [
          { name: 'url', type: 'string', required: true, description: 'Request URL' },
          { name: 'headers', type: 'json', required: false, description: 'Request headers' },
          { name: 'body', type: 'json', required: false, description: 'Request body' },
        ],
      },
      {
        id: 'patch',
        name: 'PATCH Request',
        description: 'Send HTTP PATCH request with body',
        parameters: [
          { name: 'url', type: 'string', required: true, description: 'Request URL' },
          { name: 'headers', type: 'json', required: false, description: 'Request headers' },
          { name: 'body', type: 'json', required: false, description: 'Request body' },
        ],
      },
      {
        id: 'delete',
        name: 'DELETE Request',
        description: 'Send HTTP DELETE request',
        parameters: [
          { name: 'url', type: 'string', required: true, description: 'Request URL' },
          { name: 'headers', type: 'json', required: false, description: 'Request headers' },
        ],
      },
    ],
    triggers: [
      {
        id: 'webhook',
        name: 'Webhook',
        description: 'Triggered when webhook receives a request',
        parameters: [
          { name: 'path', type: 'string', required: true, description: 'Webhook path' },
        ],
      },
    ],
  },
  {
    id: 'database',
    name: 'Database',
    category: 'database',
    icon: '🗄️',
    description: 'Execute SQL queries on PostgreSQL, MySQL, SQLite, or SQL Server',
    actions: [
      {
        id: 'query',
        name: 'Execute Query',
        description: 'Run a SQL query',
        parameters: [
          { name: 'connection', type: 'string', required: true, description: 'Connection string' },
          { name: 'query', type: 'code', required: true, description: 'SQL query' },
          { name: 'parameters', type: 'json', required: false, description: 'Query parameters' },
        ],
      },
    ],
  },
  {
    id: 'email',
    name: 'Email (SMTP)',
    category: 'communication',
    icon: '📧',
    description: 'Send emails via SMTP',
    actions: [
      {
        id: 'send',
        name: 'Send Email',
        description: 'Send an email message',
        parameters: [
          { name: 'to', type: 'string', required: true, description: 'Recipient email' },
          { name: 'subject', type: 'string', required: true, description: 'Email subject' },
          { name: 'body', type: 'string', required: true, description: 'Email body' },
          { name: 'from', type: 'string', required: false, description: 'Sender email' },
        ],
      },
    ],
  },
  {
    id: 'slack',
    name: 'Slack',
    category: 'communication',
    icon: '💬',
    description: 'Send messages to Slack channels',
    actions: [
      {
        id: 'sendMessage',
        name: 'Send Message',
        description: 'Post a message to a Slack channel',
        parameters: [
          { name: 'channel', type: 'string', required: true, description: 'Channel name or ID' },
          { name: 'text', type: 'string', required: true, description: 'Message text' },
          { name: 'token', type: 'string', required: true, description: 'Slack bot token' },
        ],
      },
    ],
  },
  {
    id: 'github',
    name: 'GitHub',
    category: 'web',
    icon: '🐙',
    description: 'Interact with GitHub repositories',
    actions: [
      {
        id: 'createIssue',
        name: 'Create Issue',
        description: 'Create a new GitHub issue',
        parameters: [
          { name: 'owner', type: 'string', required: true, description: 'Repository owner' },
          { name: 'repo', type: 'string', required: true, description: 'Repository name' },
          { name: 'title', type: 'string', required: true, description: 'Issue title' },
          { name: 'body', type: 'string', required: false, description: 'Issue description' },
          { name: 'token', type: 'string', required: true, description: 'GitHub token' },
        ],
      },
    ],
  },

  // Phase 2 - Communication
  {
    id: 'discord',
    name: 'Discord',
    category: 'communication',
    icon: '🎮',
    description: 'Send messages to Discord channels',
    actions: [
      {
        id: 'sendMessage',
        name: 'Send Message',
        description: 'Post a message to Discord',
        parameters: [
          { name: 'webhookUrl', type: 'string', required: true, description: 'Discord webhook URL' },
          { name: 'content', type: 'string', required: true, description: 'Message content' },
        ],
      },
    ],
  },
  {
    id: 'twilio',
    name: 'Twilio',
    category: 'communication',
    icon: '📱',
    description: 'Send SMS messages via Twilio',
    actions: [
      {
        // 'sendSms', not 'sendSMS' - the connector's casing is what the engine
        // matches on. accountSid/authToken are NOT action parameters: the
        // connector reads them from its ConnectorConfiguration in
        // InitializeAsync, so they belong to a connection (NodeData.credentialId).
        id: 'sendSms',
        name: 'Send SMS',
        description: 'Send an SMS message',
        parameters: [
          { name: 'to', type: 'string', required: true, description: 'Recipient phone number' },
          { name: 'body', type: 'string', required: true, description: 'Message body' },
          { name: 'from', type: 'string', required: false, description: 'Sender number (defaults to the connection\'s number)' },
        ],
      },
    ],
  },
  {
    id: 'sendgrid',
    name: 'SendGrid',
    category: 'communication',
    icon: '✉️',
    description: 'Send emails via SendGrid',
    actions: [
      {
        // 'sendEmail' is the connector's action id. The API key is not an action
        // parameter - it comes from the connection (NodeData.credentialId).
        id: 'sendEmail',
        name: 'Send Email',
        description: 'Send an email via SendGrid',
        parameters: [
          { name: 'to', type: 'string', required: true, description: 'Recipient email' },
          { name: 'subject', type: 'string', required: true, description: 'Email subject' },
          { name: 'body', type: 'string', required: true, description: 'Email body (HTML)' },
          { name: 'from', type: 'string', required: false, description: 'Sender email' },
        ],
      },
    ],
  },
  {
    id: 'telegram',
    name: 'Telegram',
    category: 'communication',
    icon: '✈️',
    description: 'Send messages via Telegram Bot',
    actions: [
      {
        id: 'sendMessage',
        name: 'Send Message',
        description: 'Send a Telegram message',
        parameters: [
          { name: 'chatId', type: 'string', required: true, description: 'Chat ID' },
          { name: 'text', type: 'string', required: true, description: 'Message text' },
          { name: 'botToken', type: 'string', required: true, description: 'Telegram Bot Token' },
        ],
      },
    ],
  },
  {
    id: 's3',
    name: 'AWS S3',
    category: 'cloud',
    icon: '☁️',
    description: 'Upload and download files from AWS S3',
    actions: [
      {
        id: 'upload',
        name: 'Upload File',
        description: 'Upload a file to S3',
        parameters: [
          { name: 'bucket', type: 'string', required: true, description: 'S3 bucket name' },
          { name: 'key', type: 'string', required: true, description: 'Object key (path)' },
          { name: 'content', type: 'string', required: true, description: 'File content' },
        ],
      },
    ],
  },

  // Phase 3 - Enterprise
  {
    id: 'redis',
    name: 'Redis',
    category: 'database',
    icon: '⚡',
    description: 'High-performance caching and data operations (10K-100K ops/sec)',
    actions: [
      {
        id: 'set',
        name: 'Set Value',
        description: 'Store a value in Redis',
        parameters: [
          { name: 'key', type: 'string', required: true, description: 'Key' },
          { name: 'value', type: 'string', required: true, description: 'Value' },
          { name: 'ttl', type: 'number', required: false, description: 'Time to live (seconds)' },
        ],
      },
      {
        id: 'get',
        name: 'Get Value',
        description: 'Retrieve a value from Redis',
        parameters: [
          { name: 'key', type: 'string', required: true, description: 'Key' },
        ],
      },
    ],
  },
  {
    // NOTE: id and action ids must match GoogleSheetsConnector exactly - the
    // engine looks handlers up by `${integration}:${action}`, so a mismatch
    // means the node fails at execution with "no handler". This entry used to
    // say 'googlesheets'/'appendRow'; the connector declares
    // 'google-sheets'/'appendValues'.
    id: 'google-sheets',
    name: 'Google Sheets',
    category: 'cloud',
    icon: '📊',
    description: 'Read and write data to Google Sheets',
    actions: [
      {
        id: 'appendValues',
        name: 'Append Values',
        description: 'Append rows to a sheet',
        parameters: [
          { name: 'spreadsheetId', type: 'string', required: true, description: 'Spreadsheet ID' },
          { name: 'range', type: 'string', required: true, description: 'Sheet name or range to append to' },
          { name: 'values', type: 'json', required: true, description: 'Row values' },
          { name: 'valueInputOption', type: 'select', required: false, description: 'How input is interpreted', options: [
            { label: 'User Entered', value: 'USER_ENTERED' },
            { label: 'Raw', value: 'RAW' },
          ]},
        ],
      },
    ],
  },
  {
    id: 'stripe',
    name: 'Stripe',
    category: 'web',
    icon: '💳',
    description: 'Process payments and manage customers',
    actions: [
      {
        id: 'createCharge',
        name: 'Create Charge',
        description: 'Process a payment',
        parameters: [
          { name: 'amount', type: 'number', required: true, description: 'Amount in cents' },
          { name: 'currency', type: 'string', required: true, description: 'Currency code' },
          { name: 'source', type: 'string', required: true, description: 'Payment source' },
          { name: 'apiKey', type: 'string', required: true, description: 'Stripe API key' },
        ],
      },
    ],
  },
  {
    id: 'webhook',
    name: 'Webhook',
    category: 'web',
    icon: '🔗',
    description: 'Send data to external webhook endpoints',
    actions: [
      {
        id: 'send',
        name: 'Send Webhook',
        description: 'POST data to a webhook URL',
        parameters: [
          { name: 'url', type: 'string', required: true, description: 'Webhook URL' },
          { name: 'payload', type: 'json', required: true, description: 'Data payload' },
        ],
      },
    ],
  },
  {
    id: 'ftp',
    name: 'FTP/SFTP',
    category: 'file',
    icon: '📁',
    description: 'Upload and download files via FTP/SFTP',
    actions: [
      {
        id: 'upload',
        name: 'Upload File',
        description: 'Upload a file to FTP/SFTP server',
        parameters: [
          { name: 'host', type: 'string', required: true, description: 'Server host' },
          { name: 'path', type: 'string', required: true, description: 'Remote path' },
          { name: 'content', type: 'string', required: true, description: 'File content' },
          { name: 'username', type: 'string', required: true, description: 'Username' },
          { name: 'password', type: 'string', required: true, description: 'Password' },
        ],
      },
    ],
  },

  // Transform
  {
    id: 'transform',
    name: 'Transform',
    category: 'transform',
    icon: '🔄',
    description: 'Transform data using C# code',
    actions: [
      {
        id: 'execute',
        name: 'Transform Data',
        description: 'Execute C# transformation code',
        parameters: [
          { name: 'code', type: 'code', required: true, description: 'C# transformation code' },
        ],
      },
    ],
  },
];

export function getIntegrationById(id: string): Integration | undefined {
  return integrations.find((i) => i.id === id);
}

export function getIntegrationsByCategory(category: string): Integration[] {
  return integrations.filter((i) => i.category === category);
}
