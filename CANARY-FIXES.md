# Canary Header Propagation Changes

## What this does

Three things are needed for coordinated canary deployments where all canary pods route to other canary pods:

1. **Header propagation** — When a request arrives with a configured header (e.g. `X-Canary: true`), that header must be forwarded on all outbound HTTP calls so downstream services are also routed to their canary pods via AGC. Uses .NET 8's built-in `HeaderPropagation` middleware + `ConfigureHttpClientDefaults` to cover all `HttpClient` instances globally.

2. **OIDC backchannel fix** — The Identity service's OpenID Connect handler creates its own internal `Backchannel` HttpClient that bypasses `IHttpClientFactory`. This is fixed by explicitly wiring the OIDC handler to use a factory-created client, ensuring the propagation handler applies to Identity → SSO backchannel calls.

3. **Response middleware** — Echoes back a routed confirmation header (e.g. `X-Canary-Routed: true`) and tags the active trace span with pod name, build hash, and canary status for DataDog observability.

All are **configuration-driven** — the header name is set via ConfigMap (`HeaderPropagation__Headers__0=X-Canary`), never hardcoded in server code. When unconfigured (e.g. self-hosted), all features are no-ops.

## Service coverage

| Service | Outbound propagation | Inbound middleware | Notes |
|---|---|---|---|
| **API** | `AddDefaultServices` | `UseDefaultMiddleware` | Makes calls to Identity, Notifications |
| **Identity** | `AddDefaultServices` | `UseDefaultMiddleware` | Makes calls to SSO (OIDC backchannel also fixed) |
| **Billing** | `AddDefaultServices` | `UseDefaultMiddleware` (added) | Receives calls from Admin |
| **Admin** | `AddDefaultServices` | `UseDefaultMiddleware` (added) | Makes calls to Billing, Identity |
| **SSO** | `AddDefaultServices` | `UseDefaultMiddleware` (added) | Receives calls from Identity |
| **SCIM** | `AddDefaultServices` | `UseDefaultMiddleware` (added) | Makes calls to Identity |
| **Events** | N/A | `UseDefaultMiddleware` | No outbound inter-service calls |
| **Notifications** | N/A | `UseDefaultMiddleware` (added) | No outbound inter-service calls |
| **Icons** | N/A | `UseDefaultMiddleware` (added) | No outbound inter-service calls |
| **EventsProcessor** | N/A | N/A | Background worker, no HTTP pipeline |

Services marked "N/A" for outbound propagation don't make inter-service HTTP calls, so they only need the inbound middleware for the response header and DataDog tagging. Services marked "(added)" did not previously call that method and were updated in this change.

`UseDefaultMiddleware` conditionally calls `UseHeaderPropagation()` only if `AddHeaderPropagation()` was registered (via `AddDefaultServices`), preventing runtime errors on services that don't make outbound HTTP calls.

## Shared resources (not isolated by canary)

Some communication channels between services are not HTTP-based. Header propagation does not and cannot isolate these. This is by design — canary routing operates at the HTTP layer only.

**Database** — Canary and stable pods read from and write to the same database. A prerequisite for canary deployments is that database migrations are decoupled from application deployments: both the current and next app versions must handle the current and next database schema. This is being validated independently.

**Azure Service Bus** — Events and application cache messages are published to shared topics/subscriptions. A canary pod may publish a message consumed by a stable pod and vice versa. Both app versions must handle messages from either version.

**Redis** — The SignalR backplane uses shared Redis. Notifications published by canary pods may be relayed through stable pod connections. Both app versions must handle notifications from either version.

These constraints apply equally to today's rolling deployments where old and new pods coexist during rollout. Canary does not introduce new data-plane risks — it extends the coexistence window.
