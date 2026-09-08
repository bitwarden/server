# Subscriptions.User

Feature-tier library exposing the account-scoped subscription HTTP surface — the endpoints an
individual user hits to preview and manage their own subscription.

See [LIBRARY.md](../LIBRARY.md) for the shape all libraries under `src/Libraries/` follow.

## Public surface

`AddUserSubscriptions()` registers the endpoint group's services and the `Bit.Invoicing` library
they depend on. `MapUserSubscriptionEndpoints()` attaches the group's cross-cutting chain — tags,
the `Application` authorization policy, exception handling, and the `PM36631_PreviewDrivenCart`
feature gate — to an empty group; the host owns the route prefix and mounts it at
`/account/billing/subscription/premium`.

### Endpoints

| Route | Handler | Returns |
| --- | --- | --- |
| `POST .../upgrade/invoice/preview` | `UserSubscriptionEndpointsHandler.PreviewPremiumOrgUpgradeAsync` | `InvoicePreview` |

`PreviewPremiumOrgUpgradeAsync` resolves the caller via `IUserService` (401 if the principal has no user),
validates the request in its `ToDomain()` (400 with the model-state body for an unsupported tier or a
malformed billing address — Minimal APIs do not run DataAnnotations, so the request model validates itself),
and runs `Bit.Invoicing`'s `IBuildInvoicePreviewForPremiumOrgUpgradeCommand` for the Families, Teams, or
Enterprise annual plan. Routes are relative to the group, so this resolves to
`POST /account/billing/subscription/premium/upgrade/invoice/preview`.

## Stripe boundary

This library never calls Stripe. It makes no Stripe API calls and never touches `IStripeAdapter`;
all Stripe interaction is delegated to `Bit.Invoicing`'s public surface. Referencing Stripe SDK
types to pass data across that surface is fine — calling Stripe from here is not.

## Core debt

This library depends on `Core` as a documented deviation from the rule restricting Libraries from referencing Core, per ADR-0032:

| From Core | Used for |
| --- | --- |
| `Policies.Application` (`Bit.Core.Auth.Identity`) | Requiring the standard user authorization policy on the group |
| `IUserService` (`Bit.Core.Services`) | Resolving the authenticated user from the request principal; Minimal APIs cannot use the MVC `[InjectUser]` filter |
| `ProductTierType`, `PlanType` (`Bit.Core.Billing.Enums`) | The request's target-tier contract and the plan type passed across the `Bit.Invoicing` surface |
| `BillingAddress` (`Bit.Core.Billing.Payment.Models`) | The billing address the tax calculation needs, passed across the `Bit.Invoicing` surface |

Depending on `Core` for these is fine for now; this table exists so they're known, not because
they're queued up for extraction.
