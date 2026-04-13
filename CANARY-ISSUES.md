# Canary Header Propagation Issues

Without header propagation, inter-service calls from canary pods lose the `X-Canary` header and fall through to stable pods via AGC. This breaks canary isolation in a coordinated deployment where all services should stay within the canary call chain.

## Issue 1: Inter-service HTTP calls lose the canary header

All cloud inter-service calls use external HTTPS URLs (e.g. `https://identity.usdev.bitwarden.pw`) which route through AGC. Without the `X-Canary` header on these outbound calls, AGC routes them to stable pods.

**Affected calls:**

**API → Identity** (token acquisition)
- `BaseIdentityClientService` calls `POST /connect/token` on Identity to get bearer tokens
- Every service that makes authenticated inter-service calls hits this path first

**API → Notifications** (push notifications)
- `NotificationsApiPushEngine` calls `POST /send` on the Notifications service
- Triggered on cipher operations, folder changes, org updates — high frequency

**Admin → Billing** (Stripe recovery)
- `ProcessStripeEventsController` calls `POST /stripe/recovery/events/*` on Billing
- Lower frequency, but would still route to stable Billing during canary

**Admin → Identity** (token acquisition)
- Same `BaseIdentityClientService` pattern as API

**Identity → SSO** (OIDC)
- Named HttpClient `"InternalSso"` in Identity's Startup
- Any SSO login flow would route to stable SSO

**Notifications → Identity** (token acquisition)
- Same `BaseIdentityClientService` token pattern

**Billing → Identity** (token acquisition)
- Same `BaseIdentityClientService` token pattern

**Highest risk:** API → Identity and API → Notifications are the most frequent calls. A canary API pod would get its token from stable Identity and send push notifications to stable Notifications — breaking the canary isolation chain.

## Issue 2: OIDC backchannel bypasses HttpClientFactory

The Identity service's OpenID Connect middleware (for SSO login flows) creates its own internal `Backchannel` HttpClient. This client is not created through `IHttpClientFactory`, so `ConfigureHttpClientDefaults` does not apply. The `X-Canary` header would not propagate on Identity → SSO backchannel calls (discovery, token exchange, userinfo).

## Issue 3: Shared resources are not isolated by canary routing

Some communication channels between services are not HTTP and cannot be isolated via header propagation:

- **Database** — Canary and stable pods share the same database. Writes from canary pods are visible to stable pods and vice versa.
- **Azure Service Bus** — Events and application cache messages are published to shared topics. A canary pod may publish a message consumed by a stable pod.
- **Redis** — SignalR backplane uses shared Redis. Notifications published by canary pods may be relayed by stable pods.

These are data-plane concerns, not routing concerns. Header propagation cannot address them.

## Resolution

See [CANARY-FIXES.md](CANARY-FIXES.md) for how Issues 1 and 2 are resolved. Issue 3 is addressed by design constraints documented there.
