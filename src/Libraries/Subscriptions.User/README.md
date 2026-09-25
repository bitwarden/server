# Subscriptions.User

Feature-tier library exposing the account-scoped subscription HTTP surface — the endpoints an
individual user hits to preview and manage their own subscription.

See [LIBRARY.md](../LIBRARY.md) for the shape all libraries under `src/Libraries/` follow.

## Public surface

`AddUserSubscriptions()` registers the group's services — a scoped handler per endpoint
(`GetAccountSubscriptionUpgradePreviewHandler`, `GetAccountSubscriptionPreviewHandler`,
`GetAccountSubscriptionPurchasePreviewHandler`) and the queries behind them
(`IGetSubscriptionUpgradePreviewQuery`, `IGetSubscriptionPurchasePreviewQuery`) — and the
`Bit.Invoicing` library they depend on. `MapUserSubscriptionEndpoints()` creates the group, applies its
cross-cutting chain (tags, the `internal` group name, the `Application` policy, exception handling,
the `PM36631_PreviewDrivenCart` feature gate), and maps the endpoints below; the host mounts it at
`/account/billing/subscription`.

Everything else is `internal`. The only types a consumer reads are `Bit.Invoicing`'s `InvoicePreview`
and `SubscriptionPreview`.

### Endpoints

| Route | Handler | Returns |
| --- | --- | --- |
| `GET .../upgrade/preview` | `GetAccountSubscriptionUpgradePreviewHandler.HandleAsync` | `InvoicePreview` |
| `GET .../preview` | `GetAccountSubscriptionPreviewHandler.HandleAsync` | `SubscriptionPreview` |
| `GET .../purchase/preview` | `GetAccountSubscriptionPurchasePreviewHandler.HandleAsync` | `InvoicePreview` |

Every preview sends `Cache-Control: no-store`. The previews are per-user and time-sensitive, so the
group applies one shared endpoint filter instead of relying on the framework's default caching
behavior.

GET endpoints are named `Get…` and POST previews are named `Preview…`, and the same prefix carries
down through the handler, query, and request (`GetAccountSubscriptionPurchasePreview`,
`GetAccountSubscriptionPurchasePreviewHandler`, `IGetSubscriptionPurchasePreviewQuery`,
`GetSubscriptionPurchasePreviewRequest`).

The upgrade preview handler resolves the caller via `IUserService` (401 if none) and runs
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
caller's own upcoming subscription renewal. It returns 404 when the caller can't be resolved or has
no Stripe subscription to preview.

The Premium purchase preview handler resolves the caller via `IUserService` (401 if none) and runs
`IGetSubscriptionPurchasePreviewQuery`. The query validates the request before any I/O, using the
same address keys and messages as the upgrade preview, then previews a new subscription: the
available Premium plan's seat at quantity 1 plus its storage when requested, with automatic tax and
no customer or subscription, so the preview reflects no existing customer balance or discount.
Coupons are all-or-nothing: they apply only when `ISubscriptionDiscountService` finds every one
eligible for the Premium discount tier, and otherwise they are dropped silently and the preview runs
without them.

Every amount comes from Stripe through `Bit.Invoicing`, and `PasswordManager.Seats` is always
present. If Stripe resolves no seats line, the query logs the user id and the price ids it sent and
returns a 409; the organization purchase preview in `Subscriptions.Organization` does the same. A
missing plan differs between the two: here `IPricingClient.GetAvailablePremiumPlan` throws
`NotFoundException`, which surfaces as a 404, while the organization preview returns a 409.

## Stripe boundary

The upgrade preview reads the user's subscription through `IStripeAdapter` to decide which items
to swap and delete. The purchase preview reads no Stripe state and only builds
`InvoiceCreatePreviewOptions`. Every flow hands its options to `Bit.Invoicing`, and the library
never calls Stripe's invoice APIs directly.

## Core debt

Documented deviation from the Libraries-do-not-reference-Core rule, per ADR-0032:

| From Core | Used for |
| --- | --- |
| `Policies.Application` (`Bit.Core.Auth.Identity`) | Authorization policy on the group |
| `IUserService`, `User` (`Bit.Core.Services`, `Bit.Core.Entities`) | Resolving the caller |
| `IStripeAdapter` (`Bit.Core.Billing.Services`) | Reading the user's subscription items |
| `IPricingClient` (`Bit.Core.Billing.Pricing`) | Premium price ids, the upgrade target plan, and the Premium purchase plan |
| `Plan` (`Bit.Core.Models.StaticStore`) | The upgrade target plan's price ids returned by `IPricingClient` |
| `ISubscriptionDiscountService`, `DiscountTierType` (`Bit.Core.Billing.Services`, `Bit.Core.Billing.Enums`) | Eligibility-checking Premium purchase coupons |
| `ProductTierType`, `PlanType`, `PlanCadenceType` (`Bit.Core.Billing.Enums`) | Request contract and the `Bit.Invoicing` call |
| `StripeConstants` (`Bit.Core.Billing.Constants`) | `always_invoice`, `classic` billing mode, `customer_tax_location_invalid`, `resource_missing` |
| `BadRequestException`, `ConflictException`, `NotFoundException` (`Bit.Core.Exceptions`) | 400, 409, and 404 responses via the group's exception handling |
