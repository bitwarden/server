# Invoicing

Owns the invoice-preview projection: turning a subscription change into a preview of what the
customer will be charged before they commit to it. Platform-tier — it is domain logic, not a host.

See [LIBRARY.md](../LIBRARY.md) for the shape all libraries under `src/Libraries/` follow.

## Public surface

`AddInvoicing()` registers everything the library needs, including the feature flag keys the library
owns (`InvoicingFeatureFlags`) as known flags. The feature
libraries above gate on those keys without depending on Core for them. The projection types land in
a later change.

## Stripe boundary

Invoicing owns the Stripe interaction behind invoice previews — the `IStripeAdapter` calls that
hydrate invoice and subscription data — and projects the results into the vendor-neutral models on
its public surface. Feature libraries above it never call Stripe themselves; they consume preview
data through this surface, and reference Stripe SDK types only to pass data to and from it.

## Core debt

This library depends on `Core` as a documented deviation from the rule restricting Libraries from referencing Core, per ADR-0032:

| From Core | Used for |
| --- | --- |
| `IStripeAdapter` | Hydrating Stripe invoice/subscription data |
| `IPricingClient` | Resolving plan and price data |
| `ISubscriptionDiscountService` | Applying discounts to the preview |
| `SponsoredPlans` | Sponsorship pricing rules |
| `ISubscriber` | The organization or user being billed |

Depending on `Core` for these is fine for now; this table exists so they're known, not because
they're queued up for extraction.
