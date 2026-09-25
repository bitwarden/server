# Subscriptions.User

Feature-tier library exposing the account-scoped subscription HTTP surface — the endpoints an
individual user hits to preview and manage their own subscription.

See [LIBRARY.md](../LIBRARY.md) for the shape all libraries under `src/Libraries/` follow.

## Public surface

`AddUserSubscriptions()` registers the group's services — a scoped handler per endpoint
(`GetAccountSubscriptionUpgradePreviewHandler`, `GetAccountSubscriptionPreviewHandler`,
`GetAccountPremiumPurchasePreviewHandler`, `GetAccountOrganizationPurchasePreviewHandler`) and the
queries behind them (`IGetSubscriptionUpgradePreviewQuery`, `IGetPremiumPurchasePreviewQuery`,
`IGetOrganizationPurchasePreviewQuery`) — and the `Bit.Invoicing` library they depend on. `MapUserSubscriptionEndpoints()` creates the group, applies its
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
| `GET .../purchase/premium/preview` | `GetAccountPremiumPurchasePreviewHandler.HandleAsync` | `InvoicePreview` |
| `POST .../purchase/organization/preview` | `GetAccountOrganizationPurchasePreviewHandler.HandleAsync` | `InvoicePreview` |

Every preview sends `Cache-Control: no-store`. The previews are per-user and time-sensitive, so the
group applies one shared endpoint filter instead of relying on the framework's default caching
behavior. The organization purchase preview is a `POST` even though it changes nothing. Its request
is a nested body, and it can carry a tax ID, which a query string would expose to access logs,
request telemetry, and proxies. The other previews are `GET`s.

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

### Purchase previews

Both purchase handlers resolve the caller via `IUserService` (401 if none) and run their query.
Each query validates the request itself, before any I/O, and throws a `BadRequestException`
keyed by the offending field. The query then builds `InvoiceCreatePreviewOptions` for a new
subscription with automatic tax, currency `usd`, and `classic` billing mode. Neither query sets a
customer, a subscription, or `Expand`. That means the preview reflects no existing customer balance
or customer discount, and `Bit.Invoicing` owns expansion. A Stripe
`customer_tax_location_invalid` becomes a 400. Any other Stripe error propagates and the group's
exception handling turns it into a logged 500.

Every amount comes from Stripe through `Bit.Invoicing`. Neither query adds, removes, or rewrites
lines or discounts in the projection. `PasswordManager.Seats` is always present on both purchase
previews. A missing seats line or a plan the pricing service does not have is a catalog fault:
logged with the user id and the price ids or plan type, and surfaced as a 409.

#### Premium purchase preview

- **Validation:** `additionalStorage` must be 0–99 (a missing value means 0). The billing address
  needs a two-letter `country` and a `postalCode`, with the same keys and messages as the upgrade
  preview.
- **Items:** the available Premium plan's seat price at quantity 1, plus its storage price at the
  requested quantity when above 0.
- **Coupons:** repeated `coupons` keys. Blank entries are dropped and the rest are trimmed. When any
  remain, they are applied only if `ISubscriptionDiscountService` finds all of them eligible for the
  Premium discount tier. Otherwise the preview runs without coupons. No error is returned and
  nothing is logged.
- The preview is projected as the Premium tier, annual cadence. If the pricing service has no
  available Premium plan, `IPricingClient.GetAvailablePremiumPlan` throws `NotFoundException`,
  which surfaces as a 404.

#### Organization purchase preview

The body mirrors the legacy purchase request: `{ purchase: { tier, cadence, passwordManager:
{ seats, additionalStorage, sponsored }, secretsManager?: { seats, additionalServiceAccounts,
standalone }, coupons? }, billingAddress: { country, postalCode, taxId?: { code, value } } }`.
`tier` and `cadence` accept either the enum name or its number. An empty or unreadable body is
rejected by the framework with a 400 before the query runs.

- **Validation (400s, keyed by path such as `Purchase.PasswordManager.Seats`):**
  - `purchase`, `purchase.passwordManager`, and `billingAddress` are required.
  - The tier must be Families, Teams, or Enterprise, and the cadence must be defined.
  - Families is annual only and has no Secrets Manager.
  - `sponsored` is allowed only on Families.
  - Password Manager seats must be 1–100,000 and storage 0–99.
  - Secrets Manager, when present, needs 1–100,000 seats and 0–100,000 service accounts.
  - The address needs a two-letter country and a postal code. A tax ID, when present, needs both a
    code and a value.
- **Plan:** Families maps to `FamiliesAnnually`. Teams and Enterprise map to their annual or monthly
  plan. The plan comes from `IPricingClient.GetPlan`. If the catalog has no plan for that type, the
  query logs an error and throws `ConflictException` (409, not 404), because the catalog missing a
  plan is a server fault the user cannot fix.
- **Sponsored Families** (`passwordManager.sponsored`): the query sends the Families-for-Enterprise
  sponsored price at quantity 1, plus the Families storage price at `additionalStorage` when above 0
  (redemption keeps the storage add-on, so it is billed and taxed). It sends no coupons. Stripe is
  authoritative here, as everywhere else: the `pm-seat` line carries the sponsored unit cost
  (normally $0), `additionalStorage` carries the storage line, and there is no synthetic
  sponsorship discount. This replaces the client's two-call storage-tax workaround (PM-27585).
- **Standalone Secrets Manager** (`secretsManager.standalone`): the query sends the Password Manager
  seats and the Secrets Manager seats. Storage and service-account items are added when above 0,
  matching what subscription creation bills (legacy dropped them). The `sm-standalone` coupon is
  always applied, user coupons are ignored, and the discount service is not called.
- **Everything else:** packaged plans (Families) send the package price at quantity 1, whatever
  `seats` says. That matches subscription creation (legacy sent `seats`). Seat-based plans send the
  seat price at `seats`. Storage, Secrets Manager seats, and service accounts are added when above 0.
  User coupons are honoured only for Families, trimmed, and applied only if all are eligible for the
  Families discount tier. Teams and Enterprise ignore them without calling the discount service.
- **Tax ID:** the Stripe tax ID type is derived with `ITaxService.GetStripeTaxCode`. If it can't be
  derived, the query falls back to the client-supplied code and logs a warning that names only the
  country and the client's code, never the tax ID value. A Spanish NIF (`es_cif`) also sends an
  `eu_vat` tax ID prefixed with `ES`. A Stripe `tax_id_invalid` becomes a 400.

## Stripe boundary

The upgrade preview reads the user's subscription through `IStripeAdapter` to decide which items
to swap and delete. The purchase previews read no Stripe state and only build
`InvoiceCreatePreviewOptions`. Every flow hands its options to `Bit.Invoicing`, and the library
never calls Stripe's invoice APIs directly.

## Core debt

Documented deviation from the Libraries-do-not-reference-Core rule, per ADR-0032:

| From Core | Used for |
| --- | --- |
| `Policies.Application` (`Bit.Core.Auth.Identity`) | Authorization policy on the group |
| `IUserService`, `User` (`Bit.Core.Services`, `Bit.Core.Entities`) | Resolving the caller |
| `IStripeAdapter` (`Bit.Core.Billing.Services`) | Reading the user's subscription items |
| `IPricingClient` (`Bit.Core.Billing.Pricing`) | Premium price ids, the upgrade target plan, and the Premium and organization purchase plans |
| `Plan` (`Bit.Core.Models.StaticStore`) | Organization plan price ids returned by `IPricingClient` |
| `ISubscriptionDiscountService`, `DiscountTierType` (`Bit.Core.Billing.Services`, `Bit.Core.Billing.Enums`) | Eligibility-checking purchase coupons |
| `ITaxService` (`Bit.Core.Billing.Tax.Services`) | Deriving the Stripe tax ID type |
| `SponsoredPlans`, `PlanSponsorshipType` (`Bit.Core.Billing.Models`, `Bit.Core.Enums`) | The Families-for-Enterprise sponsored price |
| `ProductTierType`, `PlanType`, `PlanCadenceType` (`Bit.Core.Billing.Enums`) | Request contract and the `Bit.Invoicing` call |
| `StripeConstants` (`Bit.Core.Billing.Constants`) | `always_invoice`, `classic` billing mode, the `sm-standalone` coupon, `customer_tax_location_invalid`, `tax_id_invalid`, `resource_missing`, the `es_cif` and `eu_vat` tax ID types |
| `BadRequestException`, `ConflictException` (`Bit.Core.Exceptions`) | 400 and 409 responses via the group's exception handling |
