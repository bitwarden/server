# Passkey Directory Report

## Overview

The Passkey Directory Report provides a list of domains that support passkeys, sourced from the [2FA Directory API](https://2fa.directory). This data powers a report in the Bitwarden client that helps organization administrators understand which of their members' credentials could be upgraded to passkeys.

For client-side implementation details, see the [clients README](https://github.com/bitwarden/clients/blob/d866c8126444bf95f2be2ee5f59646aa1237e8a7/apps/web/src/app/dirt/reports/pages/README.md).

## Feature Flag

This feature is gated behind the `PasskeyDirectoryReport` feature flag.

## Architecture

### Data Flow

```
2FA Directory API  -->  GetPasskeyDirectoryQuery (cached 24h)  -->  ReportsController  -->  Client
```

1. **External source**: The [2FA Directory v1 API](https://passkeys-api.2fa.directory/v1/all.json) provides a JSON dictionary of domains and their passkey/MFA support.
2. **Query layer** (`GetPasskeyDirectoryQuery`): Fetches and parses the external data, caching results for 24 hours via FusionCache.
3. **API endpoint** (`ReportsController`): Exposes `GET /reports/passkey-directory` which returns the cached directory entries.

### Key Files

| File | Purpose |
|------|---------|
| `GetPasskeyDirectoryQuery.cs` | Core query — fetches, parses, and caches the 2FA Directory data |
| `Interfaces/IGetPasskeyDirectoryQuery.cs` | Query interface |
| `ReportingServiceCollectionExtensions.cs` | DI registration for the query, HTTP client, and cache |
| `../../Models/Data/PasskeyDirectoryEntry.cs` | Domain model for a directory entry |
| `src/Api/Dirt/Controllers/ReportsController.cs` | API controller exposing the endpoint |
| `src/Api/Dirt/Models/Response/PasskeyDirectoryResponseModel.cs` | API response model |

### Caching

- **Provider**: FusionCache (keyed service `"PasskeyDirectory"`)
- **Duration**: 24 hours
- **Key**: `"passkey-directory"`
- Cache is registered in `ReportingServiceCollectionExtensions.AddReportingServices()`.

### Response Shape

Each entry in the response array contains:

| Field | Type | Description |
|-------|------|-------------|
| `domainName` | `string` | The domain (e.g. `github.com`) |
| `passwordless` | `bool` | Whether the domain supports passwordless passkey login |
| `mfa` | `bool` | Whether the domain supports passkeys as an MFA method |
| `instructions` | `string` | URL to setup documentation (empty if unavailable) |

### API Endpoint

```
GET /reports/passkey-directory
```

- **Auth**: Standard Bitwarden authentication
- **Feature flag**: `PasskeyDirectoryReport`
- **Response**: `IEnumerable<PasskeyDirectoryResponseModel>`
