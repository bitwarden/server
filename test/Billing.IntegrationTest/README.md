# Billing.IntegrationTest

These tests exercise Bitwarden's billing surface against a **real Stripe test account**. They exist to catch one specific class of bug that unit tests cannot see: a mismatch between the `Expand = [...]` paths a query asks Stripe for and the fields the C# code subsequently reads back.

## Why these tests exist

Bitwarden's billing code routinely asks the Stripe SDK to inline related objects on a single request:

```csharp
var subscription = await subscriberService.GetSubscription(
    organization,
    new SubscriptionGetOptions { Expand = ["customer.tax_ids", "latest_invoice", "test_clock"] });

// Later, the same code path reads:
var taxIds = subscription.Customer.TaxIds;          // requires "customer.tax_ids"
var status = subscription.LatestInvoice.Status;     // requires "latest_invoice"
```

`Expand` is a Stripe API convention: paths not listed in `Expand` come back as **id-only stubs**, not full objects. If a developer reads `subscription.LatestInvoice.Status` but forgets `"latest_invoice"` in `Expand`, Stripe returns the invoice id only — `LatestInvoice` is a stub with `Status == null` — and the code path silently produces a wrong answer (or a `NullReferenceException` at runtime).

Unit tests can't catch this:
- Mocked Stripe services return whatever object graph the test sets up, regardless of the `Expand` list the production code passed in.
- Static analysis can't tell from the call site which property accesses require which `Expand` paths.

This project closes the gap. Every scenario walks a billing flow end-to-end against real Stripe and asserts the response carries the fields the code expects. A missing `Expand` entry surfaces as a real test failure.

## What belongs here

Any production code path that:

1. Constructs a Stripe SDK options object with `Expand = [...]`, **and**
2. Reads at least one of the expanded fields off the returned Stripe object.

When you add or modify such a code path, add (or update) a scenario here that drives it through HTTP and asserts on the expanded data. Pure CRUD with no expand paths, or logic with no Stripe call at all, belongs in a unit test instead.

## Running

Tests gate on the `RUN_STRIPE_INTEGRATION_TESTS` environment variable (see [`BillingFactAttribute`](BillingFactAttribute.cs)). They're skipped by default so CI never spends time on real Stripe API calls.

```bash
export RUN_STRIPE_INTEGRATION_TESTS=1
# Stripe API key sourced from user-secrets in src/Identity:
#   globalSettings:stripe:apiKey = sk_test_...
dotnet test test/Billing.IntegrationTest
```

The Stripe key must be a **test-mode** key (`sk_test_...`). The tests POST organizations using the canonical Stripe test card token `pm_card_visa` and exercise real network calls; they will fail loudly against a live-mode key (and would be unsafe to run there).

## Conventions

Follow the patterns in [`test/INTEGRATION_TEST.md`](../INTEGRATION_TEST.md).
