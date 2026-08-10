# Subscriptions.User

Feature-tier library exposing the account-scoped subscription HTTP surface — the endpoints an
individual user hits to preview and manage their own subscription. Empty shell for now; endpoints
arrive with the individual screen slices.

See [LIBRARY.md](../LIBRARY.md) for the shape all libraries under `src/Libraries/` follow.

## Public surface

`AddUserSubscriptions()` registers the endpoint group's services and the `Bit.Invoicing` library
they depend on. `MapUserSubscriptionEndpoints()` maps the `/subscriptions` route group, gated
behind `PM36631_PreviewDrivenCart` and requiring the `Application` authorization policy. No
endpoints are mapped inside the group yet.

## Vendor boundary

This library is vendor-free: it consumes `Bit.Invoicing`'s public contracts for pricing and
invoice-preview data, and never references Stripe types directly.

## Core debt

This library depends on `Core` as a TL-approved interim deviation:

| From Core | Used for |
| --- | --- |
| `FeatureFlagKeys` | Gating the endpoint group behind `PM36631_PreviewDrivenCart` |
| `Policies.Application` (`Bit.Core.Auth.Identity`) | Requiring the standard user authorization policy on the group |

Depending on `Core` for these is fine for now; this table exists so they're known, not because
they're queued up for extraction.
