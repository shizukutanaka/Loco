# GraphQL API Documentation

## Overview

Loco provides a comprehensive GraphQL API alongside the REST API for advanced querying capabilities, real-time subscriptions, and efficient data fetching.

## Endpoints

- **GraphQL Endpoint**: `/graphql`
- **GraphQL Playground**: `/graphql/playground`
- **WebSocket Endpoint**: `/graphql` (for subscriptions)

## Features

### Query Operations
- Fetch flows with pagination and filtering
- Advanced search with multiple criteria
- Flow statistics and validation
- Batch data loading with DataLoader

### Mutation Operations
- Create, update, and delete flows
- Clone existing flows
- Batch operations (delete, enable/disable)
- Flow validation

### Subscription Operations
- Real-time flow change notifications
- Flow execution events
- System event monitoring

## Query Examples

### Get All Flows
```graphql
query GetFlows {
  flows(skip: 0, take: 20) {
    id
    name
    description
    enabled
    category
    tags
    createdAt
    updatedAt
  }
}
```

### Search Flows
```graphql
query SearchFlows {
  searchFlows(criteria: {
    name: "backup"
    enabled: true
    category: "automation"
    skip: 0
    take: 10
  }) {
    items {
      id
      name
      description
    }
    totalCount
    hasMore
  }
}
```

### Get Flow Details
```graphql
query GetFlow($id: String!) {
  flow(id: $id) {
    id
    name
    description
    enabled
    triggers {
      type
      config
    }
    conditions {
      type
      config
    }
    actions {
      type
      config
    }
    metadata {
      author
      version
      executionCount
      lastExecuted
    }
  }
}
```

### Get Flow Statistics
```graphql
query GetStatistics {
  flowStatistics {
    totalFlows
    enabledFlows
    disabledFlows
    flowsByCategory
    lastUpdated
  }
}
```

### Validate Flow
```graphql
query ValidateFlow($flow: FlowInput!) {
  validateFlow(flow: $flow) {
    isValid
    errors
    warnings
  }
}
```

## Mutation Examples

### Create Flow
```graphql
mutation CreateFlow {
  createFlow(flow: {
    name: "Daily Backup"
    description: "Backup important files daily"
    enabled: true
    category: "backup"
    tags: ["automation", "backup", "daily"]
    triggers: [{
      type: "time.schedule"
      config: { hour: 2, minute: 0 }
    }]
    actions: [{
      type: "file.backup"
      config: { 
        source: "/important/data"
        destination: "/backup/daily"
      }
    }]
  }) {
    id
    name
    createdAt
  }
}
```

### Update Flow
```graphql
mutation UpdateFlow($id: String!, $flow: FlowInput!) {
  updateFlow(id: $id, flow: $flow) {
    id
    name
    updatedAt
  }
}
```

### Delete Flow
```graphql
mutation DeleteFlow($id: String!) {
  deleteFlow(id: $id)
}
```

### Toggle Flow
```graphql
mutation ToggleFlow($id: String!, $enabled: Boolean!) {
  toggleFlow(id: $id, enabled: $enabled) {
    id
    enabled
    updatedAt
  }
}
```

### Clone Flow
```graphql
mutation CloneFlow($id: String!, $newName: String) {
  cloneFlow(id: $id, newName: $newName) {
    id
    name
    createdAt
  }
}
```

### Batch Operations
```graphql
mutation BatchDelete($ids: [String!]!) {
  batchDeleteFlows(ids: $ids) {
    totalCount
    successCount
    failedCount
  }
}

mutation BatchEnable($ids: [String!]!, $enabled: Boolean!) {
  batchEnableFlows(ids: $ids, enabled: $enabled) {
    totalCount
    successCount
    failedCount
  }
}
```

## Subscription Examples

### Subscribe to Flow Changes
```graphql
subscription OnFlowChanged {
  flowChanged {
    type
    flow {
      id
      name
      enabled
    }
    timestamp
  }
}
```

### Subscribe to Flow Executions
```graphql
subscription OnFlowExecuted($flowId: String) {
  flowExecuted(flowId: $flowId) {
    flowId
    executionId
    status
    timestamp
    metadata
  }
}
```

### Subscribe to System Events
```graphql
subscription OnSystemEvent {
  systemEvent {
    type
    message
    severity
    timestamp
  }
}
```

## Types

### Flow Type
```graphql
type Flow {
  id: String!
  name: String!
  description: String
  enabled: Boolean!
  category: String
  tags: [String]
  triggers: [Trigger]
  conditions: [Condition]
  actions: [Action]
  metadata: FlowMetadata
  createdAt: DateTime
  updatedAt: DateTime
}
```

### Trigger Type
```graphql
type Trigger {
  type: String!
  config: Any
}
```

### Condition Type
```graphql
type Condition {
  type: String!
  config: Any
}
```

### Action Type
```graphql
type Action {
  type: String!
  config: Any
}
```

### FlowMetadata Type
```graphql
type FlowMetadata {
  author: String
  version: String
  executionCount: Int
  lastExecuted: DateTime
}
```

### FlowSearchResult Type
```graphql
type FlowSearchResult {
  items: [Flow]!
  totalCount: Int!
  hasMore: Boolean!
}
```

### FlowStatistics Type
```graphql
type FlowStatistics {
  totalFlows: Int!
  enabledFlows: Int!
  disabledFlows: Int!
  flowsByCategory: Any
  lastUpdated: DateTime!
}
```

### FlowValidationResult Type
```graphql
type FlowValidationResult {
  isValid: Boolean!
  errors: [String]
  warnings: [String]
}
```

### BatchOperationResult Type
```graphql
type BatchOperationResult {
  totalCount: Int!
  successCount: Int!
  failedCount: Int!
}
```

## Input Types

### FlowInput
```graphql
input FlowInput {
  name: String!
  description: String
  enabled: Boolean = true
  category: String
  tags: [String]
  triggers: [TriggerInput]
  conditions: [ConditionInput]
  actions: [ActionInput]
}
```

### TriggerInput
```graphql
input TriggerInput {
  type: String!
  config: Any
}
```

### ConditionInput
```graphql
input ConditionInput {
  type: String!
  config: Any
}
```

### ActionInput
```graphql
input ActionInput {
  type: String!
  config: Any
}
```

### FlowSearchInput
```graphql
input FlowSearchInput {
  name: String
  description: String
  category: String
  enabled: Boolean
  createdAfter: DateTime
  createdBefore: DateTime
  tags: [String]
  skip: Int = 0
  take: Int = 20
}
```

### FlowSortInput
```graphql
input FlowSortInput {
  field: String
  direction: SortDirection
}
```

## Enums

### SortDirection
```graphql
enum SortDirection {
  Ascending
  Descending
}
```

## Authentication

GraphQL endpoints support the same authentication mechanisms as the REST API:

1. **JWT Bearer Token**: Include in Authorization header
2. **API Key**: Include in X-API-Key header
3. **OAuth2**: Use OAuth2 flow for authentication

Example with JWT:
```javascript
const response = await fetch('/graphql', {
  method: 'POST',
  headers: {
    'Content-Type': 'application/json',
    'Authorization': 'Bearer <your-jwt-token>'
  },
  body: JSON.stringify({
    query: '{ flows { id name } }'
  })
});
```

## Error Handling

GraphQL errors are returned in the standard format:

```json
{
  "data": null,
  "errors": [
    {
      "message": "Flow with ID xyz not found",
      "extensions": {
        "code": "NOT_FOUND",
        "timestamp": "2025-08-20T10:00:00Z"
      }
    }
  ]
}
```

## Rate Limiting

The GraphQL endpoint follows the same rate limiting rules as the REST API:
- 100 requests per minute for authenticated users
- 20 requests per minute for anonymous users

## Performance Considerations

1. **Use Field Selection**: Only request fields you need
2. **Implement Pagination**: Use skip/take for large datasets
3. **Leverage DataLoader**: Automatic batching for N+1 query prevention
4. **Monitor Query Complexity**: Complex nested queries may be limited

## Client Examples

### JavaScript/TypeScript
```javascript
import { GraphQLClient } from 'graphql-request';

const client = new GraphQLClient('/graphql', {
  headers: {
    authorization: 'Bearer YOUR_TOKEN',
  },
});

const query = `
  query GetFlows($skip: Int, $take: Int) {
    flows(skip: $skip, take: $take) {
      id
      name
      enabled
    }
  }
`;

const variables = {
  skip: 0,
  take: 10
};

const data = await client.request(query, variables);
```

### C#/.NET
```csharp
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.Newtonsoft;

var graphQLClient = new GraphQLHttpClient("/graphql", new NewtonsoftJsonSerializer());

var flowsRequest = new GraphQLRequest
{
    Query = @"
        query GetFlows($skip: Int, $take: Int) {
            flows(skip: $skip, take: $take) {
                id
                name
                enabled
            }
        }",
    Variables = new { skip = 0, take = 10 }
};

var response = await graphQLClient.SendQueryAsync<FlowsResponse>(flowsRequest);
```

### Python
```python
import requests

url = "/graphql"
headers = {"Authorization": "Bearer YOUR_TOKEN"}

query = """
    query GetFlows($skip: Int, $take: Int) {
        flows(skip: $skip, take: $take) {
            id
            name
            enabled
        }
    }
"""

variables = {"skip": 0, "take": 10}

response = requests.post(
    url, 
    json={"query": query, "variables": variables},
    headers=headers
)

data = response.json()
```

## WebSocket Subscriptions

```javascript
import { createClient } from 'graphql-ws';

const client = createClient({
  url: 'ws://localhost:5000/graphql',
  connectionParams: {
    authToken: 'YOUR_TOKEN',
  },
});

const unsubscribe = client.subscribe(
  {
    query: `
      subscription OnFlowChanged {
        flowChanged {
          type
          flow {
            id
            name
          }
          timestamp
        }
      }
    `,
  },
  {
    next: (data) => console.log('Flow changed:', data),
    error: (err) => console.error('Error:', err),
    complete: () => console.log('Subscription complete'),
  }
);

// Later, to unsubscribe
unsubscribe();
```

## Testing with GraphQL Playground

1. Navigate to `/graphql/playground`
2. The interactive IDE allows you to:
   - Write and execute queries
   - Browse the schema documentation
   - Test subscriptions
   - Save query history
   - Set HTTP headers for authentication

## Best Practices

1. **Use Fragments** for reusable field selections
2. **Implement Query Batching** for multiple operations
3. **Cache Results** on the client side
4. **Monitor Performance** using the built-in metrics
5. **Validate Input** before sending mutations
6. **Handle Errors Gracefully** in client applications
7. **Use Subscriptions Sparingly** for real-time requirements only
