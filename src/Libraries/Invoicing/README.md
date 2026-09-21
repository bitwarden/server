# Invoicing

Owns the invoice-preview projection: turning a subscription change into a preview of what the
customer will be charged before they commit to it. Platform-tier — it is domain logic, not a host.

See [LIBRARY.md](../LIBRARY.md) for the shape all libraries under `src/Libraries/` follow.

## Public surface

`AddInvoicing()` registers the projection service, the `IGetSubscriptionPreviewQuery`, and the
feature flag keys the library owns (`InvoicingFeatureFlags`) as known flags. The public surface is
`IInvoicePreviewService`, `IGetSubscriptionPreviewQuery`, and the `InvoicePreview` /
`SubscriptionPreview` record family under `InvoicePreviews/Models/` (including `PlanTierType`). The
service, builder, mappers, reference table, and Stripe client are internal.

`IGetSubscriptionPreviewQuery.Run(ISubscriber)` builds the `SubscriptionPreview` for a subscriber's
upcoming renewal: the invoice preview wrapped in the subscription-level envelope (status, storage,
cancellation, and suspension). The `Organization` path is wired; the `User`/Premium path is stubbed
for its own screen slice.

## Stripe boundary

Invoicing owns the Stripe interaction behind invoice previews — the `IStripeAdapter` calls that
hydrate invoice and subscription data — and projects the results into the vendor-neutral models on
its public surface. Feature libraries above it never call Stripe themselves; they consume preview
data through this surface, and reference Stripe SDK types only to pass data to and from it.

### Proration months come from the line period

`invoice.period_end` means different things depending on `proration_behavior`: under `always_invoice`
it is the moment of change ("now"); under Stripe's default `create_prorations` it is the current
period end. A proration line's own period is stable across both — it always spans
`[change moment, period end]` (Stripe: "For prorations, this starts when the proration was calculated,
and ends at the period end of the subscription"). So `ProrationMapper` measures the remaining term as
`line.Period.End - line.Period.Start`, never against `invoice.period_end`. Confirmed against the Stripe
docs and a live test-clock preview (annual plan: the line-span formula yields 12 months where
`line.End - invoice.period_end` would yield 1).

## Core debt

This library depends on `Core` as a documented deviation from the rule restricting Libraries from referencing Core, per ADR-0032:

| From Core | Used for |
| --- | --- |
| `IStripeAdapter` | Fetching the preview invoice from Stripe |
| `StripeConstants` | The `purchasable_reference` metadata key and reference values |
| `PlanCadenceType` | The billing cadence carried on the preview |
| `ProductType` | Routing a reference to its product family |
| `BitwardenDiscountType` | The type of a projected discount |
| `Storage` | Storage figures on the subscription preview |
| `EnumMemberJsonConverter` | Serializing the projected enums (cadence, tier, discount type) as their EnumMember string values |
| `IPricingClient` | Mapping an organization's `PlanType` to tier and cadence for the preview |
| `ISubscriber`, `Organization`, `User` | The subscriber the preview query runs for |
| `PlanType`, `ProductTierType`, `SubscriptionStatus` | Plan lookup, the `TeamsStarter → Teams` tier collapse, and the status → envelope mapping |
| `Utilities.GetSubscriptionSuspensionAsync`, `GetCurrentPeriodEnd` | Suspension timing and the next-charge date on the preview |

Depending on `Core` for these is fine for now; this table exists so they're known, not because
they're queued up for extraction.
