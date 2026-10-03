# Subscriptions.Organization

Feature-tier library exposing the organization subscription HTTP surface — the endpoints an
organization admin hits to preview and manage their organization's subscription, and the endpoint a
user hits to preview buying a new organization.

See [LIBRARY.md](../LIBRARY.md) for the shape all libraries under `src/Libraries/` follow.

## Public surface

`AddOrganizationSubscriptions()` registers the group's services — a scoped handler class per endpoint
(`GetOrganizationSubscriptionPreviewHandler`, `PreviewOrganizationPlanChangeHandler`, `PreviewOrganizationSubscriptionPurchaseHandler`), each depending
only on `IOrganizationRepository` (or `IUserService` for purchases) and the query or command it runs; the plan-change command
(`IPreviewOrganizationPlanChangeCommand`) and the purchase query (`IPreviewOrganizationSubscriptionPurchaseQuery`); the `StandaloneOrganizationOwnerRequirementHandler`
authorization handler; and the `Bit.Invoicing` library they depend on. One handler class per endpoint
avoids a shared handler that accumulates a dependency per route.

`MapOrganizationSubscriptionEndpoints()` attaches the group's cross-cutting chain and maps its
endpoints to an empty group; the host owns the route prefix and mounts it at
`/organizations/{organizationId:guid}/billing/subscription`. The chain applies the
`OrganizationSubscriptions` tag, the `internal` group name (keeps these endpoints out of the public
API spec), the authenticated `Application` policy, the `OrganizationBillingRequirement`, basic
exception handling (from `Bit.ExceptionHandling`), the `PM36631_PreviewDrivenCart` feature gate, and a
`Cache-Control: no-store` filter (previews are per-organization billing data, never cached).

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

Individual endpoints may narrow this baseline further. Both organization-scoped endpoints additionally require
`StandaloneOrganizationOwnerRequirement`, so they admit **only** an Owner of a standalone organization:
an owner of a provider-managed (MSP, reseller, or business unit) organization, and a confirmed provider user, are both
denied. This is deliberately stricter than legacy `ICurrentContext.EditSubscription`, which still admits
a provider user for a provider-managed organization; provider-managed billing is administered through the
provider surface, so none of the organization's users reach the preview here.

### Endpoints

| Route | Handler | Returns |
| --- | --- | --- |
| `GET .../preview` | `GetOrganizationSubscriptionPreviewHandler` | `SubscriptionPreview` |
| `POST .../plan-change/preview` | `PreviewOrganizationPlanChangeHandler` | `InvoicePreview` |
| `POST /organizations/billing/subscription/purchase/preview` | `PreviewOrganizationSubscriptionPurchaseHandler` | `InvoicePreview` |

GET endpoints are named `Get…` and POST previews are named `Preview…`, and the same prefix carries
down through the handler, query, and request.

Each handler resolves the organization via `IOrganizationRepository` (404 if missing).
`GetOrganizationSubscriptionPreviewHandler` runs `Bit.Invoicing`'s `IGetSubscriptionPreviewQuery` and
returns the resulting `SubscriptionPreview`. `PreviewOrganizationPlanChangeHandler` runs this library's
`IPreviewOrganizationPlanChangeCommand`, which reads the subscription through `IStripeAdapter`, composes
the plan change, and hands the finished `InvoiceCreatePreviewOptions` to `Bit.Invoicing`'s
`IInvoicePreviewService`. It prorates the change against the live subscription — matching the real
upgrade, which invoices the change immediately — or previews the new plan at full price for a Free org
with no subscription.

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
value. A missing seats line or a plan the pricing service doesn't have is a catalog fault the user
can't fix and returns a 409.

### Organization plan-change preview

Its request is a JSON body: `tier` and `cadence` (the target plan, as their `EnumMember` string values,
e.g. `enterprise`/`monthly`) and a `billingAddress` (`country`, `postalCode`, and an optional tax id).
The address is passed to Stripe via `CustomerDetails` so tax can be estimated without a stored customer —
a Free org has none until it adds a payment method. The tax id sent in the request, or the customer's
on-file tax id when the request omits one, is forwarded so a VAT-registered business is quoted the tax it
will actually be charged. Its Stripe tax-id type is derived from the value with `ITaxService`, falling
back to the submitted code when it can't be derived. A trialing subscription's change prorates to $0 mid-trial, so the preview ends
the trial in the hypothetical (`trial_end = now`) and returns what the subscriber pays once it converts.
It returns the resulting `InvoicePreview` cart.

A missing or unrecognized `tier`/`cadence` fails JSON binding with a 400 before the command runs. The
command validates the rest itself. Caller-fixable problems are **400s**: the `premium` tier, Families on
a monthly cadence, a move to the same tier (including a cadence-only change), a downgrade, a plan without Secrets Manager
support for an SM-enabled org, an invalid billing address, or a tax location or tax id Stripe rejects.
Data or Stripe state the caller
cannot influence is logged with the organization id and surfaced as a **409**: a paid org with no
subscription, a subscription Stripe no longer has, a subscription in a status other than `trialing`,
`active`, or `past_due`, a subscription whose line items don't match the current plan, or a missing
seat count. A missing organization is a **404**. These map from the `BadRequestException`,
`ConflictException`, and `NotFoundException` types in `Bit.ExceptionHandling`.

## Stripe boundary

This library **reads** Stripe data directly through `IStripeAdapter` (fetching the organization's
subscription and its customer's tax ids while building a plan-change preview). That is allowed for now:
the ideal state is that all Stripe access flows through `Bit.Invoicing`, but until a shared
`Bit.Subscriptions` library exists, customer-specific read logic lives here. The final preview call —
turning the composed `InvoiceCreatePreviewOptions` into an `InvoicePreview` — still goes through
`Bit.Invoicing`'s `IInvoicePreviewService`, and this library performs no Stripe writes.

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
| `IStripeAdapter` (`Bit.Core.Billing.Services`) | Reading the organization's subscription (and its customer's tax ids) for the plan-change preview |
| `IPricingClient` (`Bit.Core.Billing.Pricing`), `Plan` (`Bit.Core.Models.StaticStore`) | Resolving the current and target plans' Stripe price ids and seat/flat shape |
| `OrganizationSubscriptionChangeSet` (`Bit.Core.Billing.Organizations.Models`) | Composing the plan change the same way the real upgrade does, then translating it into preview line items |
| `ISubscriptionDiscountService`, `DiscountTierType` (`Bit.Core.Billing.Services`, `Bit.Core.Billing.Enums`) | Eligibility-checking Families purchase coupons |
| `ITaxService` (`Bit.Core.Billing.Tax.Services`), `StripeConstants.TaxIdType` | Deriving the Stripe tax-id code (and the Spanish-NIF EU-VAT pairing) for `CustomerDetails.TaxIds` |
| `SponsoredPlans`, `PlanSponsorshipType` (`Bit.Core.Billing.Models`, `Bit.Core.Enums`) | The Families-for-Enterprise sponsored price |
| `BillingAddress`, `TaxID` (`Bit.Core.Billing.Payment.Models`) | The address and tax id on the plan-change request |
| `PlanCadenceType`, `PlanType`, `ProductTierType` (`Bit.Core.Billing.Enums`), `StripeConstants.SubscriptionStatus` | Resolving the target plan and gating the previewable subscription statuses |
| `StripeConstants` (`Bit.Core.Billing.Constants`) | `classic` billing mode, the `sm-standalone` coupon, `customer_tax_location_invalid`, `tax_id_invalid`, the `es_cif` and `eu_vat` tax ID types |
| `BadRequestException`, `ConflictException` (`Bit.Core.Exceptions`) | Purchase preview 400 and 409 responses via the group's exception handling |

Depending on `Core` for these is fine for now; this table exists so they're known, not because
they're queued up for extraction.
