# Subscriptions.User

Feature-tier library exposing the account-scoped subscription HTTP surface — the endpoints an
individual user hits to preview and manage their own subscription.

See [LIBRARY.md](../LIBRARY.md) for the shape all libraries under `src/Libraries/` follow.

## Public surface

`AddUserSubscriptions()` registers the group's services — the scoped
`UserSubscriptionEndpointsHandler` and `IPreviewPremiumUpgradeCommand` — and the `Bit.Invoicing`
library they depend on. `MapUserSubscriptionEndpoints()` attaches the group's cross-cutting chain —
tags, the `internal` group name, the `Application` authorization policy, exception handling, and the
`PM36631_PreviewDrivenCart` feature gate — to an empty group; the host owns the route prefix and
mounts it at `/account/billing/subscription/premium`.

Everything else in the library is `internal`: the request models, the command, and the handler. The
only type a consumer reads is the `InvoicePreview` the endpoint returns, which `Bit.Invoicing` owns.

### Endpoints

| Route | Handler | Returns |
| --- | --- | --- |
| `POST .../upgrade/invoice/preview` | `UserSubscriptionEndpointsHandler.PreviewPremiumUpgradeAsync` | `InvoicePreview` |

`PreviewPremiumUpgradeAsync` resolves the caller via `IUserService` (401 if the principal has no user)
and runs `IPreviewPremiumUpgradeCommand` with the request as posted. The command validates the request
once — the target tier must be Families, Teams, or Enterprise (the upgrade is annual-only) and the
billing address needs a two-letter country and a postal code — and throws `BadRequestException` with
the offending field as the model-state key. Minimal API groups here run no DataAnnotations filter, so
validation lives in the command rather than on attributes. Caller-controllable problems (tier, address,
a non-Premium user, a tax location Stripe rejects) are 400s; subscription state the user cannot
influence (no gateway subscription, no Premium seat item on it) is logged with the subscription id and
surfaced as a 409.

The command builds the Stripe preview the way the legacy `PreviewPremiumUpgradeProrationCommand` did:
drop the Premium storage add-on, swap the seat item to the target annual plan at quantity 1,
`proration_behavior = always_invoice`, automatic tax with the supplied address. It then projects the
result through `Bit.Invoicing`'s `IInvoicePreviewService` for the target tier. Under `always_invoice`
Stripe returns only proration lines, so the resulting `InvoicePreview.PasswordManager.Seats` is null and
the prorated charge, the credit for unused Premium time, tax, and remaining months are all on
`PasswordManager.Prorations[0]`. Stripe's `customer_tax_location_invalid` is surfaced as a 400 with the
same message the legacy endpoint returned.

## Stripe boundary

This library reads the user's current Stripe subscription through `IStripeAdapter` to know which
items to swap and delete, and builds the `InvoiceCreatePreviewOptions` for the upgrade. The preview
call itself and the projection into `InvoicePreview` are delegated to `Bit.Invoicing`'s public
surface; this library never calls Stripe's invoice APIs directly.

## Core debt

This library depends on `Core` as a documented deviation from the rule restricting Libraries from referencing Core, per ADR-0032:

| From Core | Used for |
| --- | --- |
| `Policies.Application` (`Bit.Core.Auth.Identity`) | Requiring the standard user authorization policy on the group |
| `IUserService` (`Bit.Core.Services`), `User` (`Bit.Core.Entities`) | Resolving the authenticated user from the request principal |
| `IStripeAdapter` (`Bit.Core.Billing.Services`) | Reading the user's Premium subscription items |
| `IPricingClient` (`Bit.Core.Billing.Pricing`) | Resolving the Premium plan's price ids and the target organization plan |
| `ProductTierType`, `PlanType`, `PlanCadenceType` (`Bit.Core.Billing.Enums`) | The request's target-tier contract and the plan passed across the `Bit.Invoicing` surface |
| `StripeConstants` (`Bit.Core.Billing.Constants`) | `always_invoice` and the `customer_tax_location_invalid` error code |
| `BadRequestException` (`Bit.Core.Exceptions`) | Caller-input rejections the group's exception handling maps to 400 |

Depending on `Core` for these is fine for now; this table exists so they're known, not because
they're queued up for extraction.
