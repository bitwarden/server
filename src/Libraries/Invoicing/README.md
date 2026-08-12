# Invoicing

Owns the invoice-preview projection: turning a subscription change into a preview of what the
customer will be charged before they commit to it. Platform-tier — it is domain logic, not a host.

See [LIBRARY.md](../LIBRARY.md) for the shape all libraries under `src/Libraries/` follow.

## Public surface

`AddInvoicing()` registers the projection service and the feature flag keys the library owns
(`InvoicingFeatureFlags`) as known flags. The public surface is `IInvoicePreviewService`, the
`InvoicePreview` record family under `InvoicePreviews/Models/` (including `PlanTierType`), and
`InvoicePreviewException`. The service, builder, mappers, reference table, and Stripe client are
internal.

## Stripe boundary

Invoicing owns the Stripe interaction behind invoice previews — the `IStripeAdapter` calls that
hydrate invoice and subscription data — and projects the results into the vendor-neutral models on
its public surface. Feature libraries above it never call Stripe themselves; they consume preview
data through this surface, and reference Stripe SDK types only to pass data to and from it.

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

Depending on `Core` for these is fine for now; this table exists so they're known, not because
they're queued up for extraction.
