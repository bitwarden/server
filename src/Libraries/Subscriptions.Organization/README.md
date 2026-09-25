# Subscriptions.Organization

Feature-tier library exposing the organization subscription HTTP surface — the endpoints an
organization admin hits to preview and manage their organization's subscription, and the endpoint a
user hits to preview buying a new organization.

See [LIBRARY.md](../LIBRARY.md) for the shape all libraries under `src/Libraries/` follow.

## Public surface

`AddOrganizationSubscriptions()` registers the library's services — the scoped
`OrganizationSubscriptionEndpointsHandler`, the scoped `PreviewOrganizationSubscriptionPurchaseHandler`
and the `IPreviewOrganizationSubscriptionPurchaseQuery` behind it, and the
`StandaloneOrganizationOwnerRequirementHandler` authorization handler — and the `Bit.Invoicing`
library they depend on.

`MapOrganizationSubscriptionEndpoints()` attaches the group's cross-cutting chain and maps its
endpoints to an empty group; the host owns the route prefix and mounts it at
`/organizations/{organizationId:guid}/billing/subscription`. The chain applies the
`OrganizationSubscriptions` tag, the `internal` group name (keeps these endpoints out of the public
API spec), the authenticated `Application` policy, the `OrganizationBillingRequirement`, basic
exception handling (from `Bit.ExceptionHandling`), and the `PM36631_PreviewDrivenCart` feature gate.

`MapOrganizationSubscriptionPurchaseEndpoints()` attaches a second group, which the host mounts at
`/organizations/billing/subscription`. Its chain applies the `OrganizationSubscriptions` tag, the
`internal` group name, the `Application` policy, basic exception handling, the
`PM36631_PreviewDrivenCart` feature gate, and an endpoint filter that sends `Cache-Control: no-store`.
It has no `OrganizationBillingRequirement` and no organization id in its route, because the caller
is buying an organization and doesn't have one yet. It lives here rather than in
`Subscriptions.User` because libraries group endpoints by what they operate on, not by who calls
them.

### Authorization

The organization-scoped group authorizes **every** endpoint — the `Application` policy plus
`OrganizationBillingRequirement` — so handlers never repeat the access check.
`OrganizationBillingRequirement` is an `IOrganizationRequirement` from the `OrganizationAuthorization`
library, enforced via `AuthorizeAttribute<OrganizationBillingRequirement>`. It admits organization
Owners and confirmed provider users managing the organization; Admin and Custom are excluded.

Individual endpoints may narrow this baseline further. The `preview` endpoint additionally requires
`StandaloneOrganizationOwnerRequirement`, so it admits **only** an Owner of a standalone organization:
an owner of a provider-managed (MSP, reseller, or business unit) organization, and a confirmed provider user, are both
denied. This is deliberately stricter than legacy `ICurrentContext.EditSubscription`, which still admits
a provider user for a provider-managed organization; provider-managed billing is administered through the
provider surface, so none of the organization's users reach the preview here.

### Endpoints

| Route | Handler | Returns |
| --- | --- | --- |
| `GET .../preview` | `OrganizationSubscriptionEndpointsHandler.GetPreviewAsync` | `SubscriptionPreview` |
| `POST /organizations/billing/subscription/purchase/preview` | `PreviewOrganizationSubscriptionPurchaseHandler.HandleAsync` | `InvoicePreview` |

GET endpoints are named `Get…` and POST previews are named `Preview…`, and the same prefix carries
down through the handler, query, and request (`PreviewOrganizationSubscriptionPurchase`,
`PreviewOrganizationSubscriptionPurchaseHandler`, `IPreviewOrganizationSubscriptionPurchaseQuery`,
`PreviewOrganizationSubscriptionPurchaseRequest`).

`GetPreviewAsync` resolves the organization via `IOrganizationRepository` (404 if missing), runs
`Bit.Invoicing`'s `IGetSubscriptionPreviewQuery` (404 if the organization has no Stripe subscription
to preview), and returns the resulting `SubscriptionPreview`. The 404s are `NotFoundException`s
(`Bit.ExceptionHandling`), which the group's exception handling maps to `404 Not Found`.

### Organization purchase preview

The purchase preview is a `POST` even though it changes nothing. Its request is a nested body, and
it can carry a tax ID, which a query string would expose to access logs, request telemetry, and
proxies. The handler resolves the caller via `IUserService` (401 if none) and runs
`IPreviewOrganizationSubscriptionPurchaseQuery`. The query validates the request before any I/O and
rejects problems with a 400 keyed by the field's path, such as `Purchase.PasswordManager.Seats`.
`tier` and `cadence` are required for every tier, Families included, because a missing enum would
otherwise bind to its zero value. The query then previews a new subscription with automatic tax and
no customer or subscription, so the preview reflects no existing customer balance or discount.

A sponsored Families purchase sends the Families-for-Enterprise sponsored price at quantity 1 in
place of the Families package, plus the Families storage price when storage is requested, because
redemption keeps the storage add-on. It sends no coupons. Standalone Secrets Manager always applies
the `sm-standalone` coupon and ignores the request's coupons, while still billing storage and
service accounts, matching what subscription creation bills. Outside sponsorship, Families bills one
package at quantity 1 regardless of `seats`, matching subscription creation, and Teams and
Enterprise bill their seat price at `seats`. Coupons apply only to Families and are all-or-nothing:
they apply only when `ISubscriptionDiscountService` finds every one eligible for the Families
discount tier, and otherwise they are dropped silently.

The Stripe tax ID type is derived with `ITaxService`. If it can't be derived, the query falls back
to the client's code and logs a warning naming only the country and that code, never the tax ID
value. A Spanish NIF also sends the `ES`-prefixed EU VAT ID. Every amount comes from Stripe through
`Bit.Invoicing`, and `PasswordManager.Seats` is always present. A missing seats line or a plan the
pricing service doesn't have is a catalog fault the user can't fix: it is logged with the user id
and the price ids or plan type, and returned as a 409. Stripe's `customer_tax_location_invalid` and
`tax_id_invalid` become 400s. Any other Stripe error propagates, and the group's exception handling
turns it into a 500.

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
| `IProviderOrganizationRepository` (`Bit.Core.AdminConsole.Repositories`) | The provider-managed-organization check behind `StandaloneOrganizationOwnerRequirement` |
| `IUserService`, `User` (`Bit.Core.Services`, `Bit.Core.Entities`) | Resolving the purchase preview's caller |
| `IPricingClient` (`Bit.Core.Billing.Pricing`) | The plan being purchased |
| `Plan` (`Bit.Core.Models.StaticStore`) | Price ids of the plan being purchased |
| `ISubscriptionDiscountService`, `DiscountTierType` (`Bit.Core.Billing.Services`, `Bit.Core.Billing.Enums`) | Eligibility-checking Families purchase coupons |
| `ITaxService` (`Bit.Core.Billing.Tax.Services`) | Deriving the Stripe tax ID type |
| `SponsoredPlans`, `PlanSponsorshipType` (`Bit.Core.Billing.Models`, `Bit.Core.Enums`) | The Families-for-Enterprise sponsored price |
| `ProductTierType`, `PlanType`, `PlanCadenceType` (`Bit.Core.Billing.Enums`) | Purchase request contract and the `Bit.Invoicing` call |
| `StripeConstants` (`Bit.Core.Billing.Constants`) | `classic` billing mode, the `sm-standalone` coupon, `customer_tax_location_invalid`, `tax_id_invalid`, the `es_cif` and `eu_vat` tax ID types |
| `BadRequestException`, `ConflictException` (`Bit.Core.Exceptions`) | Purchase preview 400 and 409 responses via the group's exception handling |

Depending on `Core` for these is fine for now; this table exists so they're known, not because
they're queued up for extraction.
