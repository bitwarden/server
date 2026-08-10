# Invoicing

Owns the invoice-preview projection: turning a subscription change into a preview of what the
customer will be charged before they commit to it. Platform-tier — it is domain logic, not a host.

See [LIBRARY.md](../LIBRARY.md) for the shape all libraries under `src/Libraries/` follow.

## Public surface

`AddInvoicing()` registers everything the library needs. Nothing else is public yet — this is the
empty shell; the projection types land in a later change.

## Stripe boundary

This is the only library permitted to reference Stripe types directly, and only to hydrate them via
`IStripeAdapter`. Every other library and host gets invoice-preview data through this library's
public surface, never Stripe's SDK.

## Core debt

This library depends on `Core` as a TL-approved interim deviation, pending extraction into
`Bit.Integrations.Billing`:

| From Core | Used for |
| --- | --- |
| `IStripeAdapter` | Hydrating Stripe invoice/subscription data |
| `IPricingClient` | Resolving plan and price data |
| `ISubscriptionDiscountService` | Applying discounts to the preview |
| `SponsoredPlans` | Sponsorship pricing rules |
| `ISubscriber` | The organization or user being billed |

Depending on `Core` for these is fine for now; this table exists so they're known, not because
they're queued up for extraction.
