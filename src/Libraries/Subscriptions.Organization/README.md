# Subscriptions.Organization

Feature-tier library exposing the organization-scoped subscription HTTP surface — the endpoints an
organization admin hits to preview and manage their organization's subscription. Empty shell for
now; endpoints arrive with the individual screen slices.

See [LIBRARY.md](../LIBRARY.md) for the shape all libraries under `src/Libraries/` follow.

## Public surface

`AddOrganizationSubscriptions()` registers the endpoint group's services and the `Bit.Invoicing`
library they depend on. `MapOrganizationSubscriptionEndpoints()` maps the
`/organizations/{organizationId:guid}/billing` route group, gated behind
`PM36631_PreviewDrivenCart` and requiring the `Application` authorization policy. No endpoints are
mapped inside the group yet. Only an authenticated caller is required at the group level; each
handler performs its own organization billing authorization check.

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
