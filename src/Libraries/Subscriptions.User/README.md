# Subscriptions.User

Feature-tier library exposing the account-scoped subscription HTTP surface — the endpoints an
individual user hits to preview and manage their own subscription.

See [LIBRARY.md](../LIBRARY.md) for the shape all libraries under `src/Libraries/` follow.

## Public surface

`AddUserSubscriptions()` registers the group's services — the scoped
`UserSubscriptionEndpointsHandler` and `IGetSubscriptionUpgradePreviewQuery` — and the `Bit.Invoicing`
library they depend on. `MapUserSubscriptionEndpoints()` creates the group, applies its
cross-cutting chain (tags, the `internal` group name, the `Application` policy, exception handling,
the `PM36631_PreviewDrivenCart` feature gate), and maps the endpoints below; the host mounts it at
`/account/billing/subscription`.

Everything else is `internal`. The only types a consumer reads are `Bit.Invoicing`'s `InvoicePreview`
and `SubscriptionPreview`.

### Endpoints

| Route | Handler | Returns |
| --- | --- | --- |
| `GET .../upgrade/preview` | `UserSubscriptionEndpointsHandler.GetUpgradePreviewAsync` | `InvoicePreview` |
| `GET .../preview` | `UserSubscriptionEndpointsHandler.GetPreviewAsync` | `SubscriptionPreview` |

Both previews are `GET` requests and send `Cache-Control: no-store` — they are per-user and
time-sensitive, so the group applies a shared endpoint filter rather than relying on the framework's
default GET caching behavior.

The handler resolves the caller via `IUserService` (401 if none) and runs
`IGetSubscriptionUpgradePreviewQuery`. The query validates the request itself (the group runs no
DataAnnotations filter): the target tier must be Families, Teams, or Enterprise and the billing
address needs a two-letter country and a postal code. Caller-controllable problems — tier, address,
a non-Premium user, a tax location Stripe rejects — are 400s. Subscription state the user cannot
influence — no gateway subscription, a subscription Stripe no longer has, no Premium seat item on
it — is logged with the user id (and subscription id when there is one) and surfaced as a 409.

The preview drops the Premium storage add-on, swaps the seat item to the target annual plan at
quantity 1, and asks Stripe for `always_invoice` prorations with automatic tax. Stripe returns only
proration lines for that, so `InvoicePreview.PasswordManager.Seats` is null.
`PasswordManager.Prorations` carries one row per purchasable reference: the `pm-seat` row for the
plan swap, plus a `pm-storage` row holding the credit for the dropped add-on when the user had one.
Row order follows Stripe's invoice-line order, so consumers should select rows by `Reference` and
sum charge, credit, and tax across them rather than reading index 0. `EstimatedTax`, `Total`, and
`AmountDue` are invoice-level and already aggregate every row.

`GET preview` delegates to `Bit.Invoicing`'s `IGetSubscriptionPreviewQuery`, which previews the
caller's own upcoming subscription renewal. It returns 404 when the caller has no Stripe
subscription to preview.

## Stripe boundary

This library reads the user's subscription through `IStripeAdapter` to decide which items to swap
and delete, then hands the `InvoiceCreatePreviewOptions` to `Bit.Invoicing`. It never calls
Stripe's invoice APIs directly.

## Core debt

Documented deviation from the Libraries-do-not-reference-Core rule, per ADR-0032:

| From Core | Used for |
| --- | --- |
| `Policies.Application` (`Bit.Core.Auth.Identity`) | Authorization policy on the group |
| `IUserService`, `User` (`Bit.Core.Services`, `Bit.Core.Entities`) | Resolving the caller |
| `IStripeAdapter` (`Bit.Core.Billing.Services`) | Reading the user's subscription items |
| `IPricingClient` (`Bit.Core.Billing.Pricing`) | Premium price ids and the target plan |
| `ProductTierType`, `PlanType`, `PlanCadenceType` (`Bit.Core.Billing.Enums`) | Request contract and the `Bit.Invoicing` call |
| `StripeConstants` (`Bit.Core.Billing.Constants`) | `always_invoice`, `customer_tax_location_invalid`, `resource_missing` |
| `BadRequestException`, `ConflictException` (`Bit.Core.Exceptions`) | 400 and 409 responses via the group's exception handling |
