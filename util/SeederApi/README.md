# SeederApi

A web API for dynamically seeding and querying test data in the Bitwarden database during development and testing.

## Overview

The SeederApi provides HTTP endpoints to execute [Seeder](../Seeder/README.md) scenes and queries, enabling automated test data
generation and retrieval through a RESTful interface. This is particularly useful for integration testing, local
development workflows, and automated test environments.

## Architecture

The SeederApi consists of three main components:

1. **Controllers** - HTTP endpoints for seeding, querying, and managing test data
2. **Services** - Business logic for scene and query execution
3. **Models** - Request/response models for API communication

### Key Components

- **SeedController** (`/seed`) - Creates and destroys seeded test data
- **QueryController** (`/query`) - Executes read-only queries against existing data
- **InfoController** (`/alive`, `/version`) - Health check and version information
- **SceneService** - Manages scene execution and cleanup with play ID tracking
- **QueryService** - Executes read-only query operations

## How To Use

### Starting the API

```bash
cd util/SeederApi
dotnet run
```

The API will start on the configured port (typically `http://localhost:5000`).

### Seeding Data

Send a POST request to `/seed` with a scene template name and optional arguments. Include the `X-Play-Id` header to
track the seeded data for later cleanup:

```bash
curl -X POST http://localhost:5000/seed \
  -H "Content-Type: application/json" \
  -H "X-Play-Id: test-run-123" \
  -d '{
    "template": "SingleUserScene",
    "arguments": {
      "email": "test@example.com"
    }
  }'
```

**Response:**

```json
{
  "mangleMap": {
    "test@example.com": "1854b016+test@example.com",
    "42bcf05d-7ad0-4e27-8b53-b3b700acc664": "42bcf05d-7ad0-4e27-8b53-b3b700acc664"
  },
  "result": null
}
```

The `result` contains the data returned by the scene, and `mangleMap` contains ID mappings if ID mangling is enabled.
Use the `X-Play-Id` header value to later destroy the seeded data.

### Querying Data

Send a POST request to `/query` to execute read-only queries:

```bash
curl -X POST http://localhost:5000/query \
  -H "Content-Type: application/json" \
  -d '{
    "template": "EmergencyAccessInviteQuery",
    "arguments": {
      "email": "test@example.com"
    }
  }'
```

**Response:**

```json
[
  "/accept-emergency?..."
]
```

### Destroying Seeded Data

#### Delete by Play ID

Use the same play ID value you provided in the `X-Play-Id` header:

```bash
curl -X DELETE http://localhost:5000/seed/test-run-123
```

#### Delete Multiple by Play IDs

```bash
curl -X DELETE http://localhost:5000/seed/batch \
  -H "Content-Type: application/json" \
  -d '["test-run-123", "test-run-456"]'
```

#### Delete All Seeded Data

```bash
curl -X DELETE http://localhost:5000/seed
```

### Health Checks

```bash
# Check if API is alive
curl http://localhost:5000/alive

# Get API version
curl http://localhost:5000/version
```

## Creating Scenes and Queries

Scenes and queries are defined in the [Seeder](../Seeder/README.md) project. The SeederApi automatically discovers and registers all
classes implementing the scene and query interfaces.

## Configuration

The SeederApi uses the standard Bitwarden configuration system:

- `appsettings.json` - Base configuration
- `appsettings.Development.json` - Development overrides
- `dev/secrets.json` - Local secrets (database connection strings, etc.)
- User Secrets ID: `bitwarden-seeder-api`

### Required Settings

The SeederApi requires the following configuration:

- **Database Connection** - Connection string to the Bitwarden database
- **Global Settings** - Standard Bitwarden `GlobalSettings` configuration

## Play ID Tracking

Certain entities such as Users and Organizations are tracked when created by a request including a PlayId. This enables
entities to be deleted after using the  PlayId.

### The X-Play-Id Header

**Important:** All seed requests should include the `X-Play-Id` header:

```bash
-H "X-Play-Id: your-unique-identifier"
```

The play ID can be any string that uniquely identifies your test run or session. Common patterns:

### How Play ID Tracking Works

When `TestPlayIdTrackingEnabled` is enabled in GlobalSettings, the `PlayIdMiddleware`
(see `src/SharedWeb/Utilities/PlayIdMiddleware.cs:7-23`) automatically:

1. **Extracts** the `X-Play-Id` header from incoming requests
2. **Sets** the play ID in the `PlayIdService` for the request scope
3. **Tracks** all entities (users, organizations, etc.) created during the request
4. **Associates** them with the play ID in the `PlayData` table
5. **Enables** complete cleanup via the delete endpoints

This tracking works for **any API request** that includes the `X-Play-Id` header, not just SeederApi endpoints. This means
you can track entities created through:

- **Scene executions** - Data seeded via `/seed` endpoint
- **Regular API operations** - Users signing up, creating organizations, inviting members, etc.
- **Integration tests** - Any HTTP requests to the Bitwarden API during test execution

Without the `X-Play-Id` header, entities will not be tracked and cannot be cleaned up using the delete endpoints.

## Security Considerations

> [!WARNING]
> The SeederApi is intended for **development and testing environments only**. Never deploy this API to production
> environments.
