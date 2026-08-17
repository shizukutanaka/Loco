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
          // 'from' removed: EmailConnector takes the sender from its credentials
          // (fromEmail/fromName), not from action parameters.
          { name: 'cc', type: 'string', required: false, description: 'CC recipients' },
          { name: 'isHtml', type: 'boolean', required: false, description: 'Send as HTML' },
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
          // No 'token' here: SlackConnector reads its bot token from
          // ConnectorConfiguration (GetCredentialString("botToken")), never as an
          // action parameter, so offering it did nothing except invite a secret
          // into the workflow JSON. Credentials come from NodeData.credentialId.
          { name: 'channel', type: 'string', required: true, description: 'Channel name or ID' },
          { name: 'text', type: 'string', required: true, description: 'Message text' },
          { name: 'threadTs', type: 'string', required: false, description: 'Thread timestamp to reply in' },
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
          // 'token' removed: it is a credential (GetCredentialString("token")),
          // not an action parameter.
          { name: 'labels', type: 'json', required: false, description: 'Issue labels' },
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
          // 'sendMessage' posts to a channel as the bot (auth via the botToken
          // credential) and takes channelId. 'webhookUrl' belongs to the separate
          // 'sendWebhookMessage' action - the two were conflated here.
          { name: 'channelId', type: 'string', required: true, description: 'Discord channel ID' },
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
          // SendGridConnector reads 'html' and 'text', never 'body'.
          { name: 'html', type: 'string', required: false, description: 'HTML body' },
          { name: 'text', type: 'string', required: false, description: 'Plain-text body' },
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
          // The connector uploads from a path ('filePath'), not inline content.
          { name: 'filePath', type: 'string', required: true, description: 'Local file path to upload' },
          { name: 'contentType', type: 'string', required: false, description: 'MIME type' },
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
          // RedisConnector's parameter is 'expirySeconds'; 'ttl' is a separate
          // ACTION on that connector, and was never read here.
          { name: 'expirySeconds', type: 'number', required: false, description: 'Time to live (seconds)' },
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
          // 'apiKey' removed: StripeConnector authenticates with the credential
          // GetCredentialString("secretKey") from its configuration.
          { name: 'description', type: 'string', required: false, description: 'Charge description' },
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

  // Variables. Registered by the engine as "variable:set" / "variable:get", so
  // unlike transform/condition/delay/loop (which the engine dispatches by node
  // TYPE) these resolve through the normal `${integration}:${action}` path and
  // therefore belong in this list.
  {
    id: 'variable',
    name: 'Variable',
    category: 'transform',
    icon: '📦',
    description: 'Store and read workflow variables',
    actions: [
      {
        id: 'set',
        name: 'Set Variable',
        description: 'Store a value under a name for later nodes to read',
        parameters: [
          { name: 'name', type: 'string', required: true, description: 'Variable name' },
          { name: 'value', type: 'string', required: true, description: 'Value to store' },
        ],
      },
      {
        id: 'get',
        name: 'Get Variable',
        description: 'Read a previously stored value',
        parameters: [
          { name: 'name', type: 'string', required: true, description: 'Variable name' },
        ],
      },
    ],
  },
  // NOTE: there is deliberately no 'transform' entry here. The engine dispatches
  // transform by node TYPE, so an entry in this list would produce
  // integration='transform', type='action' and a handler key of
  // "transform:<action>" that is registered nowhere - the node would fail at
  // execution. The working Transform node is the Logic-section drag source in
  // NodePalette, which sets type='transform'. (The removed entry also advertised
  // "Execute C# transformation code"; the engine has no C# execution.)
  // ---------------------------------------------------------------------------
  // Connector-backed integrations.
  //
  // ids and action ids below are taken verbatim from the C# connectors in
  // src/Loco.Core/Integrations/Connectors, because the engine resolves a node
  // by `${integration}:${action}`. integrations.contract.test.ts reads those
  // sources and fails if any id here drifts from them.
  //
  // Credentials are NOT parameters: connectors receive them via
  // ConnectorConfiguration/InitializeAsync, referenced by NodeData.credentialId.
  // ---------------------------------------------------------------------------
  {
    id: 'airtable',
    name: 'Airtable',
    category: 'database',
    icon: '🗃️',
    description: 'Spreadsheet-database hybrid for organizing and sharing data',
    actions: [
      {
        id: 'listRecords',
        name: 'List Records',
        description: 'List records from a table',
        parameters: [
          { name: 'baseId', type: 'string', required: true, description: 'baseId' },
          { name: 'tableId', type: 'string', required: true, description: 'Table ID or Name' },
          { name: 'view', type: 'string', required: false, description: 'view' },
          { name: 'filterByFormula', type: 'string', required: false, description: 'Airtable formula to filter records' },
          { name: 'maxRecords', type: 'number', required: false, description: 'maxRecords' },
          { name: 'pageSize', type: 'number', required: false, description: 'pageSize' },
          { name: 'sort', type: 'json', required: false, description: 'sort' },
          { name: 'fields', type: 'string', required: false, description: 'Comma-separated field names' },
          { name: 'offset', type: 'string', required: false, description: 'Pagination offset' },
        ],
      },
      {
        id: 'getRecord',
        name: 'Get Record',
        description: 'Get a single record by ID',
        parameters: [
          { name: 'baseId', type: 'string', required: true, description: 'baseId' },
          { name: 'tableId', type: 'string', required: true, description: 'tableId' },
          { name: 'recordId', type: 'string', required: true, description: 'recordId' },
        ],
      },
      {
        id: 'createRecord',
        name: 'Create Record',
        description: 'Create a new record in a table',
        parameters: [
          { name: 'baseId', type: 'string', required: true, description: 'baseId' },
          { name: 'tableId', type: 'string', required: true, description: 'tableId' },
          { name: 'fields', type: 'json', required: true, description: 'Field values as key-value pairs' },
          { name: 'typecast', type: 'boolean', required: false, description: 'Auto-convert string values' },
        ],
      },
    ],
  },
  {
    id: 'azure-blob-storage',
    name: 'Azure Blob Storage',
    category: 'cloud',
    icon: '☁️',
    description: 'Microsoft Azure cloud object storage service for unstructured data',
    actions: [
      {
        id: 'listContainers',
        name: 'List Containers',
        description: 'List all containers in the storage account',
        parameters: [
          { name: 'prefix', type: 'string', required: false, description: 'Filter by container name prefix' },
          { name: 'maxResults', type: 'number', required: false, description: 'maxResults' },
        ],
      },
      {
        id: 'createContainer',
        name: 'Create Container',
        description: 'Create a new container',
        parameters: [
          { name: 'containerName', type: 'string', required: true, description: 'containerName' },
          { name: 'publicAccess', type: 'string', required: false, description: 'container, blob, or none (default)' },
        ],
      },
      {
        id: 'deleteContainer',
        name: 'Delete Container',
        description: 'Delete a container and all its blobs',
        parameters: [
          { name: 'containerName', type: 'string', required: true, description: 'containerName' },
        ],
      },
    ],
  },
  {
    id: 'calendly',
    name: 'Calendly',
    category: 'web',
    icon: '📅',
    description: 'Scheduling platform for booking meetings and appointments',
    actions: [
      {
        id: 'getCurrentUser',
        name: 'Get Current User',
        description: 'Get information about the authenticated user',
        parameters: [],
      },
      {
        id: 'getEventTypes',
        name: 'Get Event Types',
        description: 'List all event types for a user',
        parameters: [
          { name: 'userUri', type: 'string', required: false, description: 'User URI (uses current user if not specified)' },
          { name: 'active', type: 'boolean', required: false, description: 'Filter by active status' },
        ],
      },
      {
        id: 'getEventType',
        name: 'Get Event Type',
        description: 'Get details of a specific event type',
        parameters: [
          { name: 'eventTypeUri', type: 'string', required: true, description: 'Event type URI' },
        ],
      },
    ],
  },
  {
    id: 'hubspot',
    name: 'HubSpot',
    category: 'web',
    icon: '🤝',
    description: 'CRM platform for marketing, sales, and customer service',
    actions: [
      {
        id: 'createContact',
        name: 'Create Contact',
        description: 'Create a new contact',
        parameters: [
          { name: 'email', type: 'string', required: true, description: 'email' },
          { name: 'firstName', type: 'string', required: false, description: 'firstName' },
          { name: 'lastName', type: 'string', required: false, description: 'lastName' },
          { name: 'phone', type: 'string', required: false, description: 'phone' },
          { name: 'company', type: 'string', required: false, description: 'company' },
          { name: 'website', type: 'string', required: false, description: 'website' },
          { name: 'lifecycleStage', type: 'string', required: false, description: 'subscriber, lead, marketingqualifiedlead, salesqualifiedlead, opportunity, customer, evangelist' },
          { name: 'properties', type: 'json', required: false, description: 'Additional properties' },
        ],
      },
      {
        id: 'getContact',
        name: 'Get Contact',
        description: 'Get a contact by ID',
        parameters: [
          { name: 'contactId', type: 'string', required: true, description: 'contactId' },
          { name: 'properties', type: 'string', required: false, description: 'Comma-separated property names' },
        ],
      },
      {
        id: 'updateContact',
        name: 'Update Contact',
        description: 'Update an existing contact',
        parameters: [
          { name: 'contactId', type: 'string', required: true, description: 'contactId' },
          { name: 'properties', type: 'json', required: true, description: 'properties' },
        ],
      },
    ],
  },
  {
    id: 'intercom',
    name: 'Intercom',
    category: 'communication',
    icon: '💬',
    description: 'Customer messaging platform for support, marketing, and engagement',
    actions: [
      {
        id: 'getContacts',
        name: 'Get Contacts',
        description: 'List all contacts',
        parameters: [
          { name: 'perPage', type: 'number', required: false, description: 'Results per page (max 150)' },
        ],
      },
      {
        id: 'getContact',
        name: 'Get Contact',
        description: 'Get a specific contact',
        parameters: [
          { name: 'contactId', type: 'string', required: true, description: 'contactId' },
        ],
      },
      {
        id: 'createContact',
        name: 'Create Contact',
        description: 'Create or update a contact',
        parameters: [
          { name: 'email', type: 'string', required: false, description: 'email' },
          { name: 'phone', type: 'string', required: false, description: 'phone' },
          { name: 'name', type: 'string', required: false, description: 'name' },
          { name: 'userId', type: 'string', required: false, description: 'External user ID' },
          { name: 'customAttributes', type: 'json', required: false, description: 'Custom attributes as JSON object' },
        ],
      },
    ],
  },
  {
    id: 'jira',
    name: 'Jira',
    category: 'web',
    icon: '📋',
    description: 'Create and manage issues, projects, sprints, and workflows in Jira',
    actions: [
      {
        id: 'createIssue',
        name: 'Create Issue',
        description: 'Create a new issue (bug, story, task, etc.)',
        parameters: [
          { name: 'projectKey', type: 'string', required: false, description: 'Project key (uses default if not specified)' },
          { name: 'issueType', type: 'string', required: true, description: 'Issue type: Bug, Story, Task, Epic, etc.' },
          { name: 'summary', type: 'string', required: true, description: 'summary' },
          { name: 'description', type: 'string', required: false, description: 'description' },
          { name: 'priority', type: 'select', required: false, description: 'priority' },
          { name: 'assignee', type: 'string', required: false, description: 'Assignee account ID' },
          { name: 'labels', type: 'json', required: false, description: '[\\' },
          { name: 'parentKey', type: 'string', required: false, description: 'Parent issue key for subtasks' },
        ],
      },
      {
        id: 'getIssue',
        name: 'Get Issue',
        description: 'Get issue details by key',
        parameters: [
          { name: 'issueKey', type: 'string', required: true, description: 'e.g., PROJ-123' },
          { name: 'fields', type: 'string', required: false, description: 'Comma-separated field names' },
        ],
      },
      {
        id: 'updateIssue',
        name: 'Update Issue',
        description: 'Update an existing issue',
        parameters: [
          { name: 'issueKey', type: 'string', required: true, description: 'issueKey' },
          { name: 'summary', type: 'string', required: false, description: 'summary' },
          { name: 'description', type: 'string', required: false, description: 'description' },
          { name: 'priority', type: 'string', required: false, description: 'priority' },
          { name: 'assignee', type: 'string', required: false, description: 'assignee' },
          { name: 'labels', type: 'json', required: false, description: 'labels' },
        ],
      },
    ],
  },
  {
    id: 'linear',
    name: 'Linear',
    category: 'web',
    icon: '📐',
    description: 'Issue tracking and project management for modern software teams',
    actions: [
      {
        id: 'getIssues',
        name: 'Get Issues',
        description: 'Get all issues',
        parameters: [
          { name: 'first', type: 'number', required: false, description: 'Number of issues to fetch' },
          { name: 'teamId', type: 'string', required: false, description: 'Filter by team ID' },
          { name: 'assigneeId', type: 'string', required: false, description: 'Filter by assignee ID' },
          { name: 'state', type: 'string', required: false, description: 'Filter by state name' },
        ],
      },
      {
        id: 'getIssue',
        name: 'Get Issue',
        description: 'Get a specific issue',
        parameters: [
          { name: 'issueId', type: 'string', required: true, description: 'Issue ID or identifier (e.g., ENG-123)' },
        ],
      },
      {
        id: 'createIssue',
        name: 'Create Issue',
        description: 'Create a new issue',
        parameters: [
          { name: 'title', type: 'string', required: true, description: 'title' },
          { name: 'description', type: 'string', required: false, description: 'description' },
          { name: 'teamId', type: 'string', required: true, description: 'teamId' },
          { name: 'priority', type: 'number', required: false, description: '0=No priority, 1=Urgent, 2=High, 3=Medium, 4=Low' },
          { name: 'assigneeId', type: 'string', required: false, description: 'assigneeId' },
          { name: 'projectId', type: 'string', required: false, description: 'projectId' },
          { name: 'labelIds', type: 'string', required: false, description: 'Comma-separated label IDs' },
          { name: 'estimate', type: 'number', required: false, description: 'Story points estimate' },
          { name: 'dueDate', type: 'string', required: false, description: 'dueDate' },
        ],
      },
    ],
  },
  {
    id: 'mongodb',
    name: 'MongoDB',
    category: 'database',
    icon: '🍃',
    description: 'NoSQL document database operations: CRUD, aggregation, Atlas features',
    actions: [
      {
        id: 'findOne',
        name: 'Find One',
        description: 'Find a single document',
        parameters: [
          { name: 'collection', type: 'string', required: true, description: 'collection' },
          { name: 'filter', type: 'json', required: true, description: '{\\' },
          { name: 'projection', type: 'json', required: false, description: '{\\' },
          { name: 'database', type: 'string', required: false, description: 'database' },
        ],
      },
      {
        id: 'find',
        name: 'Find Many',
        description: 'Find multiple documents',
        parameters: [
          { name: 'collection', type: 'string', required: true, description: 'collection' },
          { name: 'filter', type: 'json', required: false, description: 'filter' },
          { name: 'projection', type: 'json', required: false, description: 'projection' },
          { name: 'sort', type: 'json', required: false, description: '{\\' },
          { name: 'limit', type: 'number', required: false, description: 'limit' },
          { name: 'skip', type: 'number', required: false, description: 'skip' },
          { name: 'database', type: 'string', required: false, description: 'database' },
        ],
      },
      {
        id: 'insertOne',
        name: 'Insert One',
        description: 'Insert a single document',
        parameters: [
          { name: 'collection', type: 'string', required: true, description: 'collection' },
          { name: 'document', type: 'json', required: true, description: 'document' },
          { name: 'database', type: 'string', required: false, description: 'database' },
        ],
      },
    ],
  },
  {
    id: 'mysql',
    name: 'MySQL',
    category: 'database',
    icon: '🐬',
    description: 'MySQL/MariaDB database connector for queries, transactions, and data operations',
    actions: [
      {
        id: 'query',
        name: 'Execute Query',
        description: 'Execute a SELECT query and return results',
        parameters: [
          { name: 'sql', type: 'code', required: true, description: 'SQL query' },
          { name: 'parameters', type: 'json', required: false, description: 'Query parameters' },
          { name: 'timeout', type: 'number', required: false, description: 'Query timeout in seconds' },
        ],
      },
      {
        id: 'execute',
        name: 'Execute Command',
        description: 'Execute INSERT, UPDATE, DELETE, or DDL command',
        parameters: [
          { name: 'sql', type: 'code', required: true, description: 'sql' },
          { name: 'parameters', type: 'json', required: false, description: 'parameters' },
          { name: 'timeout', type: 'number', required: false, description: 'timeout' },
        ],
      },
      {
        id: 'scalar',
        name: 'Execute Scalar',
        description: 'Execute query and return single value',
        parameters: [
          { name: 'sql', type: 'code', required: true, description: 'sql' },
          { name: 'parameters', type: 'json', required: false, description: 'parameters' },
        ],
      },
    ],
  },
  {
    id: 'notion',
    name: 'Notion',
    category: 'web',
    icon: '📝',
    description: 'All-in-one workspace for notes, docs, wikis, and databases',
    actions: [
      {
        id: 'createPage',
        name: 'Create Page',
        description: 'Create a new page in a database or as a child of another page',
        parameters: [
          { name: 'parentType', type: 'string', required: true, description: 'database_id or page_id' },
          { name: 'parentId', type: 'string', required: true, description: 'parentId' },
          { name: 'properties', type: 'json', required: true, description: 'Page properties' },
          { name: 'children', type: 'json', required: false, description: 'Initial page content blocks' },
          { name: 'icon', type: 'string', required: false, description: 'Emoji or URL' },
        ],
      },
      {
        id: 'getPage',
        name: 'Get Page',
        description: 'Retrieve a page by ID',
        parameters: [
          { name: 'pageId', type: 'string', required: true, description: 'pageId' },
        ],
      },
      {
        id: 'updatePage',
        name: 'Update Page',
        description: 'Update page properties',
        parameters: [
          { name: 'pageId', type: 'string', required: true, description: 'pageId' },
          { name: 'properties', type: 'json', required: false, description: 'properties' },
          { name: 'archived', type: 'boolean', required: false, description: 'archived' },
        ],
      },
    ],
  },
  {
    id: 'postgresql',
    name: 'PostgreSQL',
    category: 'database',
    icon: '🐘',
    description: 'PostgreSQL database connector for queries, transactions, and data operations',
    actions: [
      {
        id: 'query',
        name: 'Execute Query',
        description: 'Execute a SELECT query and return results',
        parameters: [
          { name: 'sql', type: 'code', required: true, description: 'SQL query to execute' },
          { name: 'parameters', type: 'json', required: false, description: 'Query parameters (object with parameter names as keys)' },
          { name: 'timeout', type: 'number', required: false, description: 'Query timeout in seconds' },
        ],
      },
      {
        id: 'execute',
        name: 'Execute Command',
        description: 'Execute INSERT, UPDATE, DELETE, or DDL command',
        parameters: [
          { name: 'sql', type: 'code', required: true, description: 'SQL command to execute' },
          { name: 'parameters', type: 'json', required: false, description: 'Command parameters' },
          { name: 'timeout', type: 'number', required: false, description: 'Command timeout in seconds' },
        ],
      },
      {
        id: 'scalar',
        name: 'Execute Scalar',
        description: 'Execute query and return single value (COUNT, MAX, etc.)',
        parameters: [
          { name: 'sql', type: 'code', required: true, description: 'SQL query returning single value' },
          { name: 'parameters', type: 'json', required: false, description: 'Query parameters' },
        ],
      },
    ],
  },
  {
    id: 'salesforce',
    name: 'Salesforce',
    category: 'web',
    icon: '☁️',
    description: 'World\'s #1 CRM platform for sales, service, and marketing',
    actions: [
      {
        id: 'createLead',
        name: 'Create Lead',
        description: 'Create a new lead',
        parameters: [
          { name: 'firstName', type: 'string', required: false, description: 'firstName' },
          { name: 'lastName', type: 'string', required: true, description: 'lastName' },
          { name: 'company', type: 'string', required: true, description: 'company' },
          { name: 'email', type: 'string', required: false, description: 'email' },
          { name: 'phone', type: 'string', required: false, description: 'phone' },
          { name: 'title', type: 'string', required: false, description: 'title' },
          { name: 'status', type: 'string', required: false, description: 'status' },
          { name: 'leadSource', type: 'string', required: false, description: 'leadSource' },
        ],
      },
      {
        id: 'getLead',
        name: 'Get Lead',
        description: 'Get a lead by ID',
        parameters: [
          { name: 'leadId', type: 'string', required: true, description: 'leadId' },
        ],
      },
      {
        id: 'updateLead',
        name: 'Update Lead',
        description: 'Update an existing lead',
        parameters: [
          { name: 'leadId', type: 'string', required: true, description: 'leadId' },
          { name: 'fields', type: 'json', required: true, description: 'Fields to update' },
        ],
      },
    ],
  },
  {
    id: 'shopify',
    name: 'Shopify',
    category: 'web',
    icon: '🛍️',
    description: 'E-commerce platform for online stores and retail point-of-sale systems',
    actions: [
      {
        id: 'listProducts',
        name: 'List Products',
        description: 'Get all products from your store',
        parameters: [
          { name: 'limit', type: 'number', required: false, description: 'Number of products to return (max 250)' },
          { name: 'productType', type: 'string', required: false, description: 'productType' },
          { name: 'vendor', type: 'string', required: false, description: 'vendor' },
          { name: 'status', type: 'string', required: false, description: 'active, archived, or draft' },
        ],
      },
      {
        id: 'getProduct',
        name: 'Get Product',
        description: 'Get a specific product by ID',
        parameters: [
          { name: 'productId', type: 'string', required: true, description: 'productId' },
        ],
      },
      {
        id: 'createProduct',
        name: 'Create Product',
        description: 'Create a new product',
        parameters: [
          { name: 'title', type: 'string', required: true, description: 'title' },
          { name: 'bodyHtml', type: 'string', required: false, description: 'Description (HTML)' },
          { name: 'vendor', type: 'string', required: false, description: 'vendor' },
          { name: 'productType', type: 'string', required: false, description: 'productType' },
          { name: 'tags', type: 'string', required: false, description: 'Comma-separated tags' },
          { name: 'status', type: 'string', required: false, description: 'status' },
          { name: 'price', type: 'number', required: false, description: 'price' },
          { name: 'sku', type: 'string', required: false, description: 'sku' },
          { name: 'inventoryQuantity', type: 'number', required: false, description: 'inventoryQuantity' },
        ],
      },
    ],
  },
  {
    id: 'teams',
    name: 'Microsoft Teams',
    category: 'communication',
    icon: '👥',
    description: 'Send messages, cards, and notifications to Microsoft Teams channels',
    actions: [
      {
        id: 'sendMessage',
        name: 'Send Channel Message',
        description: 'Send a message to a Teams channel',
        parameters: [
          { name: 'teamId', type: 'string', required: false, description: 'Team ID (uses default if not specified)' },
          { name: 'channelId', type: 'string', required: false, description: 'Channel ID (uses default if not specified)' },
          { name: 'message', type: 'string', required: true, description: 'Message content (supports HTML)' },
          { name: 'importance', type: 'select', required: false, description: 'importance' },
        ],
      },
      {
        id: 'sendWebhook',
        name: 'Send Webhook Message',
        description: 'Send a message via incoming webhook (no OAuth required)',
        parameters: [
          { name: 'webhookUrl', type: 'string', required: false, description: 'Webhook URL (uses configured if not specified)' },
          { name: 'message', type: 'string', required: true, description: 'message' },
          { name: 'title', type: 'string', required: false, description: 'title' },
          { name: 'themeColor', type: 'string', required: false, description: 'themeColor' },
        ],
      },
      {
        id: 'sendAdaptiveCard',
        name: 'Send Adaptive Card',
        description: 'Send a rich adaptive card to a channel',
        parameters: [
          { name: 'teamId', type: 'string', required: false, description: 'teamId' },
          { name: 'channelId', type: 'string', required: false, description: 'channelId' },
          { name: 'card', type: 'json', required: true, description: 'Adaptive card JSON' },
        ],
      },
    ],
  },
  {
    id: 'trello',
    name: 'Trello',
    category: 'web',
    icon: '📌',
    description: 'Visual project management with boards, lists, and cards',
    actions: [
      {
        id: 'getBoards',
        name: 'Get Boards',
        description: 'Get all boards for the authenticated user',
        parameters: [
          { name: 'filter', type: 'string', required: false, description: 'all, open, closed, starred' },
        ],
      },
      {
        id: 'getBoard',
        name: 'Get Board',
        description: 'Get details of a specific board',
        parameters: [
          { name: 'boardId', type: 'string', required: true, description: 'boardId' },
        ],
      },
      {
        id: 'createBoard',
        name: 'Create Board',
        description: 'Create a new board',
        parameters: [
          { name: 'name', type: 'string', required: true, description: 'name' },
          { name: 'desc', type: 'string', required: false, description: 'desc' },
          { name: 'defaultLists', type: 'boolean', required: false, description: 'defaultLists' },
          { name: 'prefs_background', type: 'string', required: false, description: 'prefs_background' },
        ],
      },
    ],
  },
  {
    id: 'zendesk',
    name: 'Zendesk',
    category: 'web',
    icon: '🎫',
    description: 'Customer service platform for support tickets and help desk',
    actions: [
      {
        id: 'createTicket',
        name: 'Create Ticket',
        description: 'Create a new support ticket',
        parameters: [
          { name: 'subject', type: 'string', required: true, description: 'subject' },
          { name: 'description', type: 'string', required: true, description: 'description' },
          { name: 'requesterId', type: 'string', required: false, description: 'requesterId' },
          { name: 'requesterEmail', type: 'string', required: false, description: 'Required if requesterId not provided' },
          { name: 'requesterName', type: 'string', required: false, description: 'requesterName' },
          { name: 'assigneeId', type: 'string', required: false, description: 'assigneeId' },
          { name: 'groupId', type: 'string', required: false, description: 'groupId' },
          { name: 'priority', type: 'string', required: false, description: 'low, normal, high, urgent' },
          { name: 'type', type: 'string', required: false, description: 'problem, incident, question, task' },
          { name: 'status', type: 'string', required: false, description: 'status' },
          { name: 'tags', type: 'string', required: false, description: 'Comma-separated tags' },
          { name: 'customFields', type: 'json', required: false, description: 'customFields' },
        ],
      },
      {
        id: 'getTicket',
        name: 'Get Ticket',
        description: 'Get a ticket by ID',
        parameters: [
          { name: 'ticketId', type: 'string', required: true, description: 'ticketId' },
        ],
      },
      {
        id: 'updateTicket',
        name: 'Update Ticket',
        description: 'Update an existing ticket',
        parameters: [
          { name: 'ticketId', type: 'string', required: true, description: 'ticketId' },
          { name: 'subject', type: 'string', required: false, description: 'subject' },
          { name: 'status', type: 'string', required: false, description: 'status' },
          { name: 'priority', type: 'string', required: false, description: 'priority' },
          { name: 'assigneeId', type: 'string', required: false, description: 'assigneeId' },
          { name: 'groupId', type: 'string', required: false, description: 'groupId' },
          { name: 'tags', type: 'string', required: false, description: 'tags' },
          { name: 'customFields', type: 'json', required: false, description: 'customFields' },
        ],
      },
    ],
  },
  {
    id: 'zoom',
    name: 'Zoom',
    category: 'communication',
    icon: '📹',
    description: 'Video conferencing and webinar platform',
    actions: [
      {
        id: 'getUsers',
        name: 'Get Users',
        description: 'List users in the account',
        parameters: [
          { name: 'status', type: 'string', required: false, description: 'active, inactive, pending' },
          { name: 'pageSize', type: 'number', required: false, description: 'Results per page (max 300)' },
        ],
      },
      {
        id: 'getUser',
        name: 'Get User',
        description: 'Get details of a specific user',
        parameters: [
          { name: 'userId', type: 'string', required: true, description: 'User ID or email' },
        ],
      },
      {
        id: 'createUser',
        name: 'Create User',
        description: 'Create a new user',
        parameters: [
          { name: 'email', type: 'string', required: true, description: 'email' },
          { name: 'type', type: 'number', required: true, description: '1=Basic, 2=Licensed, 3=On-prem' },
          { name: 'firstName', type: 'string', required: false, description: 'firstName' },
          { name: 'lastName', type: 'string', required: false, description: 'lastName' },
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
