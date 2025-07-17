# Configurable Workflow Engine

A minimal backend service implementing a configurable state-machine API for workflow management.

## Quick Start

### Prerequisites
- .NET 8 SDK

### Running the Application
```bash
dotnet run
```

The API will be available at `http://localhost:5000` (or the port shown in console output).

## API Endpoints

### Workflow Definitions

#### Create Workflow Definition
```
POST /api/workflows
Content-Type: application/json

{
  "name": "Order Processing",
  "states": [
    {
      "id": "pending",
      "name": "Pending",
      "isInitial": true,
      "isFinal": false,
      "enabled": true,
      "description": "Order is pending approval"
    },
    {
      "id": "approved",
      "name": "Approved",
      "isInitial": false,
      "isFinal": false,
      "enabled": true
    },
    {
      "id": "completed",
      "name": "Completed",
      "isInitial": false,
      "isFinal": true,
      "enabled": true
    }
  ],
  "actions": [
    {
      "id": "approve",
      "name": "Approve Order",
      "enabled": true,
      "fromStates": ["pending"],
      "toState": "approved"
    },
    {
      "id": "complete",
      "name": "Complete Order",
      "enabled": true,
      "fromStates": ["approved"],
      "toState": "completed"
    }
  ]
}
```

#### Get Workflow Definition
```
GET /api/workflows/{id}
```

#### List All Workflow Definitions
```
GET /api/workflows
```

### Workflow Instances

#### Start Workflow Instance
```
POST /api/workflows/{definitionId}/instances
```

#### Get Workflow Instance
```
GET /api/instances/{id}
```

#### List All Workflow Instances
```
GET /api/instances
```

#### Execute Action
```
POST /api/instances/{id}/execute
Content-Type: application/json

{
  "actionId": "approve"
}
```

## Core Concepts

### State
- `id`: Unique identifier
- `name`: Human-readable name
- `isInitial`: Whether this is the starting state
- `isFinal`: Whether this is a terminal state
- `enabled`: Whether state is active
- `description`: Optional description

### Action (Transition)
- `id`: Unique identifier
- `name`: Human-readable name
- `enabled`: Whether action can be executed
- `fromStates`: List of source states
- `toState`: Target state

### Workflow Definition
- Collection of states and actions
- Must have exactly one initial state
- All referenced states must exist

### Workflow Instance
- References a workflow definition
- Tracks current state
- Maintains execution history

## Validation Rules

### Definition Validation
- No duplicate state or action IDs
- Exactly one initial state required
- All action references must point to valid states

### Runtime Validation
- Actions must be enabled
- Current state must be in action's `fromStates`
- Cannot execute actions on final states
- Action must belong to instance's definition

## Example Usage

1. **Create a workflow definition** with states and actions
2. **Start an instance** from the definition
3. **Execute actions** to move through states
4. **Inspect** current state and history

## Implementation Details

### Architecture
- Minimal API with ASP.NET Core
- In-memory storage (Dictionary-based)
- Single-file implementation for simplicity
- Dependency injection for service layer

### Key Design Decisions
- **In-memory persistence**: Simple dictionary storage for definitions and instances
- **Minimal API**: Leverages .NET 8 minimal API features for concise endpoints
- **Single service class**: Consolidates all workflow operations
- **Comprehensive validation**: Enforces all state-machine rules
- **History tracking**: Maintains action execution history with timestamps

### Assumptions
- **No authentication/authorization**: Focus on core workflow functionality
- **No concurrent access protection**: Single-threaded operations assumed
- **No persistence across restarts**: In-memory storage only
- **Basic error handling**: Returns appropriate HTTP status codes with error messages

### Known Limitations
- **No database persistence**: Data lost on restart
- **No transaction support**: No rollback capabilities
- **No advanced querying**: Simple list operations only
- **No workflow versioning**: Single version per definition
- **No bulk operations**: Individual entity operations only

### Extensions for Production
- Database persistence (Entity Framework)
- Distributed caching (Redis)
- Event sourcing for audit trails
- Workflow scheduling and timers
- User management and permissions
- API versioning and documentation
- Comprehensive logging and monitoring
- Horizontal scaling support

## Testing

The implementation includes comprehensive validation and error handling. Test scenarios:

1. **Valid workflow creation** with proper states and actions
2. **Invalid definitions** (missing initial state, duplicate IDs)
3. **Instance execution** with valid and invalid actions
4. **State transitions** following workflow rules
5. **Final state handling** (no actions allowed)

## Error Handling

The API returns appropriate HTTP status codes:
- `200 OK`: Successful operations
- `201 Created`: Resource creation
- `400 Bad Request`: Validation errors
- `404 Not Found`: Resource not found

Error responses include descriptive messages explaining the validation failure.
