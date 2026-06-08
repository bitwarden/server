# PM-38729 — Multiple discounts not reflected in business 2020-migration renewal email

**Ticket:** [PM-38729](https://bitwarden.atlassian.net/browse/PM-38729) (QA Bug, sub-task of PM-37070)
**Team:** Billing · **Sprint:** Billing 2026.11 · **Bug category:** Missed requirement
**Date:** 2026-06-08

## Problem

The business plan 2020-migration renewal email itemizes and totals only the single coupon
referenced by `cohort.ProactiveDiscountCouponCode`. Discounts attached to the Stripe
subscription itself, or carried on the subscription schedule's phases, are silently omitted
— from both the itemized discount lines and the quoted total.

Observed in the field: a subscription with a 20% cohort coupon (on the schedule phase) plus a
5% subscription-level coupon. Stripe's upcoming invoice computes both (total $26.60), but the
email shows only the 20% line and a $28 total. The customer is **over-quoted** in the email
relative to what Stripe will actually charge.

A customer can legitimately hold two discounts at once: the migration proactive discount
(cohort, on the phase) and a redeemed churn discount from
[PM-37173](https://bitwarden.atlassian.net/browse/PM-37173), which writes a coupon onto the
subscription or a schedule phase on Accept. This is a real scenario, not a synthetic test case.

## Root cause

In `src/Billing/Services/Implementations/UpcomingInvoiceHandler.cs`:

- `SendBusinessRenewalEmailAsync` (~line 439) calls `ResolveDiscountsAsync(cohort, organization)`
  (~line 468) as its only discount source.
- `ResolveDiscountsAsync` (~line 520) reads exactly one input — `cohort.ProactiveDiscountCouponCode`
  (~line 524) — and returns a `List<Discount>` with at most one element.
- The downstream math (~lines 470–477: percentages summed additively, then fixed amounts
  subtracted), the `DiscountLines` binding (~line 489), `FormatCurrency` `.00` trimming
  (~line 494), and the per-year/per-month total are **already multi-discount-ready** and asserted
  by existing PM-37070 render tests.

The defect is entirely in the **data source**, not the template or math.

## Scope

**In scope:** rework `ResolveDiscountsAsync` to resolve the union of all discount sources, plus
tests. **Out of scope:** template/`.hbs` changes, the discount math, currency formatting, the
per-year/per-month logic, and any adjacent code (Milestone2/3 coupon paths, `ResolveSeatCount`).
This matches the "missed requirement" classification — tightest scope that fixes the bug.

## Design decisions

| Decision | Choice | Rationale |
| --- | --- | --- |
| `duration="once"` coupons on a recurring total | **Apply all discounts; match Stripe's upcoming invoice** | Email total equals the customer's nearest real charge ($26.60 in the repro) and the ticket's stated expected result. `Coupon.Duration` is read into the mapping but not used to filter, leaving a future duration-filter as a one-line change. |
| Which schedule phase to read | **The post-renewal phase** (first phase with `EndDate > now`) | The email is a "your price is changing at renewal" notice; the forward-looking phase is what the customer will be billed. Matches the repro where the 20% cohort coupon sits on the upcoming phase. |
| Dedup precedence when a coupon id appears in multiple sources | **Cohort → subscription → phase, first-seen-wins** | Cohort is the migration's own discount and is already fetched today, so its display stays canonical and current behavior is preserved. Stable, intuitive line order. |
| Number of sources read | **All three (union)** — Approach A | Only option that structurally cannot silently miss a discount; the defect is a silent-omission bug, so completeness is the point. Incremental cost over a two-source approach is small and uses existing patterns. |

**Open question for the PO (Micah Edelblut), non-blocking:** the email quotes a recurring-period
total, but a `once` coupon only reduces the first invoice. Quoting the discounted total on a
recurring-total email slightly under-represents subsequent-period cost. We are proceeding with
"match the upcoming invoice" (the customer's nearest real charge); confirm this is acceptable or
whether `once` coupons should be footnoted instead.

## Solution

### Signature change

```csharp
private async Task<List<Discount>> ResolveDiscountsAsync(
    OrganizationPlanMigrationCohort cohort,
    Subscription subscription,
    Organization organization)
```

The single caller at ~line 468 becomes `await ResolveDiscountsAsync(cohort, subscription, organization)`.

### Source resolution (union + dedup)

Build a `List<Discount>` from three sources in fixed order, tracking seen coupon ids in a
`HashSet<string>` (guard against null/empty ids before adding):

1. **Cohort coupon** — existing `GetCouponAsync(cohort.ProactiveDiscountCouponCode)` path,
   unchanged. Record its coupon id.
2. **Subscription discounts** — iterate `subscription.Discounts ?? []`. Each entry carries an
   already-expanded `.Coupon` (the subscription is fetched in `HandleAsync` with
   `Expand = [..., "subscriptions.data.discounts"]`), so map directly with **no extra fetch**.
   Skip ids already seen.
3. **Post-renewal phase discounts** — reuse the schedule-lookup pattern from
   `EnableAutomaticTaxAsync` (~line 830):
   - `ListSubscriptionSchedulesAsync(new SubscriptionScheduleListOptions { Customer = subscription.CustomerId })`
   - `activeSchedule = schedules.Data.FirstOrDefault(s => s.SubscriptionId == subscription.Id && s.Status == SubscriptionScheduleStatus.Active)`
   - `now = subscription.TestClock?.FrozenTime ?? DateTime.UtcNow`
   - Select the first phase with `phase.EndDate > now` (the post-renewal phase).
   - For each `phase.Discounts[].CouponId` not already seen, resolve via `GetCouponAsync`, then map.

The returned list flows unchanged into the existing math (~lines 470–477) and `DiscountLines`
binding (~line 489).

### Per-coupon mapping helper

Extract the percent-vs-amount mapping (currently inlined at ~lines 547–557) into a local function
shared by all three sources. Internal to `ResolveDiscountsAsync`; not a refactor of surrounding code.

```csharp
Discount? MapCoupon(Coupon? coupon)
{
    if (coupon?.PercentOff is { } percentOff)
        return new Discount(IsPercentage: true, Value: percentOff, Display: $"{percentOff}%");

    if (coupon?.AmountOff is { } amountOffMinorUnits)
    {
        var amountOff = amountOffMinorUnits / 100M; // Stripe reports minor units (cents)
        return new Discount(IsPercentage: false, Value: amountOff, Display: FormatCurrency(amountOff, culture));
    }

    // neither PercentOff nor AmountOff — log Error and skip (no fabricated line)
    return null;
}
```

Preserves today's exact behavior: percent vs. amount handling, cents→dollars conversion, `.00`
trimming via `FormatCurrency`. Subscription discounts pass an expanded `Coupon` straight in; phase
discounts pass only a `CouponId`, so they `GetCouponAsync` first, then `MapCoupon`.

### Error handling

A missed discount over-quotes the customer (the alerting-worthy direction), so every failure logs
at `Error`, degrades gracefully, and never throws out of the method — a partial-discount email
beats no email.

1. **Cohort coupon fetch** — keep the existing `try/catch (StripeException)` and its
   "will quote the undiscounted price" `Error` log. On failure, skip this source and continue to
   sources 2 and 3.
2. **Schedule list/fetch** — `try/catch (StripeException)`; on failure log `Error` and skip the
   phase source (cohort + subscription discounts still returned). "No active schedule" is a clean
   no-op, not an error.
3. **Per-phase-coupon fetch** — wrap each `GetCouponAsync` individually; one coupon failing logs
   `Error` and skips that coupon, the rest still resolve. Mirror the existing message style
   including `exception.StripeError?.Code`.
4. **Neither PercentOff nor AmountOff** — keep the existing `Error` log + skip (in `MapCoupon`).

Only coupon ids, org id, and subscription id are logged — no PII, no secrets — same as today.

## Test plan

**Location:** `test/Billing.Test/Services/UpcomingInvoiceHandlerTests.cs` — hand-rolled
NSubstitute style (this suite does not use `SutProvider`/`BitAutoData`; match the convention).
Extend `BuildBusinessFixture` to optionally seed `subscription.Discounts` and register an active
schedule with phases via `ListSubscriptionSchedulesAsync`, keeping the parameterless behavior so
existing tests are untouched.

New cases (all with `PM35215_BusinessPlanPriceMigration` enabled):

1. **Ticket repro** — 20% cohort on phase + 5% on subscription → 2 discount lines, total reflects
   both. The regression test for this defect.
2. **Dedup** — cohort coupon id also present on subscription/phase → resolved once, no
   double-subtraction.
3. **Subscription-only** — `ProactiveDiscountCouponCode = null`, subscription has a coupon → one
   line (today shows nothing).
4. **Phase-only** — discount only on the post-renewal phase → resolved via `GetCouponAsync` on the
   phase coupon id.
5. **Mixed % + fixed across sources** — assert percentages-first-then-fixed ordering and `.00`
   trimming on the fixed line.
6. **Once vs forever** — both itemized and applied (locks in "match upcoming invoice").
7. **No-discount regression** — no cohort coupon, empty subscription discounts, no schedule →
   `!HasDiscount`, full price, `GetCouponAsync` not called.
8. **Phase-coupon fetch fails** — that coupon omitted, others itemized, `Error` logged, email
   still sent.
9. **No active schedule** — falls back to cohort + subscription, no exception.

**Phase selection coverage:** at least one test with a completed prior phase (`EndDate <= now`)
plus a future phase, asserting we read the future phase's discounts — proving the
"first phase with `EndDate > now`" selection.

Existing PM-37070 render tests (single-discount, `.00` trimming, per-year/per-month) cover the
template side and must stay green unchanged — no template/`.hbs` work.

## Risk / blast radius

- `ResolveDiscountsAsync` is private, called only at ~line 468 inside `SendBusinessRenewalEmailAsync`,
  reachable only from `ScheduleBusinessPlanPriceMigrationAsync` — gated by
  `FeatureFlagKeys.PM35215_BusinessPlanPriceMigration` and the Teams/Enterprise tier branch.
  Blast radius is exactly the business 2020-migration renewal email behind the flag.
- Customer-facing direction is safe: today over-quotes (omits non-cohort discounts) → fix corrects
  toward Stripe's actual charge. The only new risk is under-quoting a `once` coupon on a recurring
  total — covered by the PO open question.
- No DB, API, client, schema, or migration changes. Pure in-process Billing logic. All
  collaborators already injected — no new DI registrations. No new adapter methods required
  (`GetCouponAsync`, `ListSubscriptionSchedulesAsync` already exist).
- Cross-feature note for the PR: the renewal email will now reflect coupons written by the
  PM-37173 churn-offer flow; flag this so that team is aware.

## Files touched

- `src/Billing/Services/Implementations/UpcomingInvoiceHandler.cs` — the fix
  (`ResolveDiscountsAsync` + caller at ~line 468); reuse the schedule pattern at ~line 830.
- `test/Billing.Test/Services/UpcomingInvoiceHandlerTests.cs` — new cases; extend
  `BuildBusinessFixture`; existing single-discount render tests stay green.

No changes to `IStripeAdapter`, the mail view model, or templates.
