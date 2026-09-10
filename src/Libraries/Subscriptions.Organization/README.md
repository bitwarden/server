# Subscriptions.Organization

Feature-tier library exposing the organization-scoped subscription HTTP surface — the endpoints an
organization admin hits to preview and manage their organization's subscription.

See [LIBRARY.md](../LIBRARY.md) for the shape all libraries under `src/Libraries/` follow.

## Public surface

`AddOrganizationSubscriptions()` registers the group's services — the scoped
`OrganizationSubscriptionEndpointsHandler` — and the `Bit.Invoicing` library they depend on.

`MapOrganizationSubscriptionEndpoints()` attaches the group's cross-cutting chain and maps its
endpoints to an empty group; the host owns the route prefix and mounts it at
`/organizations/{organizationId:guid}/billing/subscription`. The chain applies the
`OrganizationSubscriptions` tag, the `internal` group name (keeps these endpoints out of the public
API spec), the authenticated `Application` policy, the `OrganizationBillingRequirement`, basic
exception handling (from `Bit.ExceptionHandling`), and the `PM36631_PreviewDrivenCart` feature gate.

### Authorization

The group authorizes **every** endpoint — the `Application` policy plus
`OrganizationBillingRequirement` — so handlers never repeat the access check.
`OrganizationBillingRequirement` is an `IOrganizationRequirement` from the `OrganizationAuthorization`
library, enforced via `AuthorizeAttribute<OrganizationBillingRequirement>`. It admits organization
Owners and confirmed provider users managing the organization; Admin and Custom are excluded.

### Endpoints

| Route | Handler | Returns |
| --- | --- | --- |
| `GET .../preview` | `OrganizationSubscriptionEndpointsHandler.GetPreviewAsync` | `SubscriptionPreview` |

`GetPreviewAsync` resolves the organization via `IOrganizationRepository` (404 if missing), runs
`Bit.Invoicing`'s `IGetSubscriptionPreviewQuery` (404 if the organization has no Stripe subscription
to preview), and returns the resulting `SubscriptionPreview`. The 404s are `NotFoundException`s
(`Bit.ExceptionHandling`), which the group's exception handling maps to `404 Not Found`.

## Stripe boundary

This library never calls Stripe. It makes no Stripe API calls and never touches `IStripeAdapter`;
all Stripe interaction is delegated to `Bit.Invoicing`'s public surface. Referencing Stripe SDK
types to pass data across that surface is fine — calling Stripe from here is not.

## Core debt

This library depends on `Core` as a documented deviation from the rule restricting Libraries from referencing Core, per ADR-0032:

| From Core | Used for |
| --- | --- |
| `Policies.Application` (`Bit.Core.Auth.Identity`) | Requiring the standard user authorization policy on the group |
| `IOrganizationRepository` (`Bit.Core.Repositories`) | Resolving the organization the preview is for |
| `Organization` (`Bit.Core.AdminConsole.Entities`) | The subscriber passed to the preview query |
| `CurrentContextOrganization` (`Bit.Core.Context`), `OrganizationUserType` (`Bit.Core.Enums`) | Evaluating the org-billing requirement (Owner vs. confirmed provider user) |

Depending on `Core` for these is fine for now; this table exists so they're known, not because
they're queued up for extraction.
