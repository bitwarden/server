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
4. **Jobs** - Scheduled jobs run through JobsHostedService.

### Key Components

- **SeedController** (`/seed`) - Creates and destroys seeded test data
- **QueryController** (`/query`) - Executes read-only queries against existing data
- **InfoController** (`/alive`, `/version`) - Health check and version information
- **SceneService** - Manages scene execution and cleanup with play ID tracking
- **QueryService** - Executes read-only query operations
- **Cleanup Job** - Executes every 15 minutes to delete old play data.

## How To Use

### Starting the API

```bash
cd util/SeederApi
dotnet run
```

The API will start on the configured port (typically `http://localhost:5000`).

### Seeding Data

Send a POST request to `/seed` with a scene template name and optional arguments. Include the `X-Play-Id` header to
track the seeded data for later cleanup. You need to set the password argument to a master password for the mock user account that is at least 8-characters. You should always use an email address with a top-level domain of example.com (see RFC 2606 — guaranteed unresolvable for more details).

```bash
curl -X POST http://localhost:5000/seed \
  -H "Content-Type: application/json" \
  -H "X-Play-Id: test-run-123" \
  -d '{
    "template": "SingleUserScene",
    "arguments": {
      "email": "test@example.com",
      "password": "REPLACE_ME"
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

### Seeding a Parameterized Organization

`SingleOrganizationScene` seeds an organization on a chosen plan and links an existing user as a confirmed owner
(seed the owner first via `SingleUserScene` and pass its `userId` as `ownerUserId`).

Beyond `planType` and `seats`, the request accepts:

- `overrides`: optional capability/collection-management flags applied **on top of** the plan defaults. Any flag left
  unset keeps the plan default. This includes `useSecretsManager`: to seed an SM-off Enterprise org, send
  `overrides.useSecretsManager: false` together with `enableSecretsManager: false`. If both
  `enableSecretsManager: true` and `overrides.useSecretsManager: false` are sent, Secrets Manager stays on. Seat
  provisioning (`smSeats` / `smServiceAccounts`) applies only when `enableSecretsManager: true`.
- `gateway`, `gatewayCustomerId`, `gatewaySubscriptionId` — billing gateway identity, so the seeded org resembles a
  real billed org.

Enum fields (`planType`, `gateway`) must be sent as their **numeric value**. In the example
below, `planType: 0` is `Free` and `gateway: 0` is `Stripe`.

```bash
curl -X POST http://localhost:5000/seed \
  -H "Content-Type: application/json" \
  -H "X-Play-Id: test-run-123" \
  -d '{
    "template": "SingleOrganizationScene",
    "arguments": {
      "ownerUserId": "42bcf05d-7ad0-4e27-8b53-b3b700acc664",
      "planType": 0,
      "name": "Acme",
      "domain": "acme.example",
      "seats": 5,
      "overrides": {
        "useSso": true,
        "useGroups": true
      },
      "gateway": 0,
      "gatewayCustomerId": "cus_123",
      "gatewaySubscriptionId": "sub_456"
    }
  }'
```

### Seeding a User with Billing Gateway Identity

`SingleUserScene` seeds a standalone user. Beyond `email` and `password`, the request accepts:

- `premium` — when true, marks the account premium (enables 1 GB storage and sets a premium expiration).
- `gateway`, `gatewayCustomerId`, `gatewaySubscriptionId` — billing gateway identity, so the seeded user resembles a
  real premium cloud user linked to a Stripe (or other gateway) customer/subscription. Any field left unset leaves the
  user's existing value unchanged.
- `kdfIterations`: PBKDF2 iteration count, from 5,000 to 2,000,000. Defaults to 5,000 for fast seeding; use 600,000
  for production-realistic logins.
- `enrollInEmail2Fa` — when true, enrolls the user in email two-factor authentication, delivered to the (mangled)
  account email. The response includes `twoFactorRecoveryCode` so the recovery login flow can be tested.

Enum fields (`gateway`) must be sent as their **numeric value**. In the example below, `gateway: 0` is `Stripe`.

```bash
curl -X POST http://localhost:5000/seed \
  -H "Content-Type: application/json" \
  -H "X-Play-Id: test-run-123" \
  -d '{
    "template": "SingleUserScene",
    "arguments": {
      "email": "premium@example.com",
      "password": "REPLACE_ME",
      "premium": true,
      "gateway": 0,
      "gatewayCustomerId": "cus_123",
      "gatewaySubscriptionId": "sub_456"
    }
  }'
```

### Creating Real Stripe Billing for a Seeded Organization or User

By default the SeederApi makes no Stripe calls. The `gateway*` fields above link identifiers the caller created
itself. Two opt-in scenes instead create the Stripe customer and subscription in the shared test account, the same
way the CLI's `--stripe-billing` does. Seed the organization or user first, then post its ID to the billing scene.

Each scene refuses an organization or user that already has a gateway ID, so it bills once. The organization scene
also refuses a provider-managed client, which its provider bills. The arguments mirror the CLI flags; see the
[card and address catalogs](../SeederUtility/README.md#cards) for the valid names.

| Scene                            | Argument                                 | Effect                                                              |
| -------------------------------- | ---------------------------------------- | ------------------------------------------------------------------- |
| `OrganizationStripeBillingScene` | `organizationId` (required)              | Organization to bill. Not a Free plan; no gateway ID set.           |
|                                  | `skipTrial` (default `false`)            | Subscription starts `active` and charges the test card immediately. |
|                                  | `trialDays` (default `30`)               | Trial length, 1–30. Not valid with `skipTrial`.                     |
|                                  | `card`, `address` (default `visa`, `us`) | Test card and billing address.                                      |
| `UserStripeBillingScene`         | `userId` (required)                      | User to bill as premium. No gateway ID set.                         |
|                                  | `card`, `address` (default `visa`, `us`) | Test card and billing address. An address with a tax ID is refused. |

```bash
curl -X POST http://localhost:5000/seed \
  -H "Content-Type: application/json" \
  -H "X-Play-Id: test-run-123" \
  -d '{
    "template": "OrganizationStripeBillingScene",
    "arguments": {
      "organizationId": "<organizationId from SingleOrganizationScene>",
      "trialDays": 7,
      "card": "mastercard",
      "address": "de-vat"
    }
  }'
```

**Response:**

```json
{
  "mangleMap": {},
  "result": {
    "organizationId": "0f5a3c3e-6d1b-4a7e-9b8e-b3b700acc664",
    "gatewayCustomerId": "cus_QxYz123",
    "gatewaySubscriptionId": "sub_1PqRs456"
  }
}
```

```bash
curl -X POST http://localhost:5000/seed \
  -H "Content-Type: application/json" \
  -H "X-Play-Id: test-run-123" \
  -d '{
    "template": "UserStripeBillingScene",
    "arguments": {
      "userId": "<userId from SingleUserScene>"
    }
  }'
```

**Response:**

```json
{
  "mangleMap": {},
  "result": {
    "userId": "42bcf05d-7ad0-4e27-8b53-b3b700acc664",
    "gatewayCustomerId": "cus_QxYz789",
    "gatewaySubscriptionId": "sub_1PqRs012",
    "premiumExpirationDate": "2027-10-20T14:03:11Z"
  }
}
```

**Prerequisites.** The same three checks as the [CLI](../SeederUtility/README.md#prerequisites) run before any
Stripe call: a `sk_test_` key at `globalSettings:stripe:apiKey` in the `bitwarden-seeder-api` user secrets,
`globalSettings:pricingUri` (shipped in `appsettings.Development.json`), and `globalSettings:selfHosted` false.

**Caveats.** `DELETE /seed/{playId}` removes database rows only; the Stripe customer and subscription stay until
Stripe's test-mode auto-cancel, about 90 days. The shared account has rate limits, so bill only what a test needs.
When Stripe fails part-way, the error's `details` names any customer already created; cancel it in Stripe. A user
can then be billed again. An organization keeps that customer ID, so the scene refuses it; seed a fresh organization
instead. An organization whose first charge failed (`card: "fails-on-charge"` with `skipTrial`) shows a blank
billing page unless the `pm-36631-preview-driven-cart` flag is on.

### Cloud-only and Self-hosted-only Scenes

Scenes refuse to seed data that cannot exist on the instance type the Seeder API runs as
(`globalSettings:selfHosted`). A refused request returns HTTP 400 and writes nothing.

| Scene | Rejected on a self-hosted Seeder API |
| --- | --- |
| `SingleProviderScene`, `ProviderUserScene` | Always: providers are cloud-only |
| `MigrationCohortExportScene` | Always: plan migration cohorts are cloud-only |
| `SingleOrganizationScene`, `SingleUserScene` | When `gatewayCustomerId` or `gatewaySubscriptionId` is set |
| `OrganizationStripeBillingScene`, `UserStripeBillingScene` | Always: Stripe billing requires cloud mode |

In the other direction, `SingleUserScene` with `selfHosted: true` and `premium: true` is rejected on a cloud Seeder
API, because a self-hosted premium license can only be written on a self-hosted instance.

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
["/accept-emergency?..."]
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

Deletes seeded data tagged with a play ID older than the provided date. Date is optional and defaults to 1 day prior to the current time of the request.

```bash
curl -X DELETE http://localhost:5000/seed \
  -H "Content-Type: application/json" \
  -d '"2026-03-23T10:45:47.0690009-10:00"'
```

#### PlayData is ephemeral

A scheduled job runs every fifteen minutes that deletes data tagged with a play ID older than 1 day. Any data you want to persist for an extended period of time must not be tagged with a play ID.

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
- **Distributed Cache** - Required for queries that read codes from the persistent distributed cache (e.g.
  `UserEmailTokenCodeQuery`, which reads email 2FA / user-verification OTP codes). The SeederApi must share the
  **same cache backend** as the server (Identity/Api) that generated the code.

#### Shared Distributed Cache

Code-reading queries look up the code in the persistent keyed `IDistributedCache`, so they only succeed when
the SeederApi and the code-generating server point at the same backend. Configure a shared backend under
`globalSettings.distributedCache`:

```json
"globalSettings": {
  "distributedCache": {
    "redis": { "connectionString": "<same Redis connection as Identity/Api>" }
  }
}
```

Cosmos DB (`distributedCache.cosmos.connectionString`) or, for self-hosted deployments, the SQL Server
`dbo.Cache` table are the other shared-backend options.

> [!IMPORTANT]
> Without a shared backend, SeederApi falls back to an in-memory cache that is local to the process, so
> code-reading queries return `Found: false` even though the code was generated elsewhere. See
> `AddDistributedCache` in `src/SharedWeb/Utilities/ServiceCollectionExtensions.cs` for the backend selection
> logic.

## Play ID Tracking

Certain entities such as Users and Organizations are tracked when created by a request including a PlayId. This enables
entities to be deleted after using the PlayId.

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
4. **Associates** them with the play ID in the `PlayItem` table
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
