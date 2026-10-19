# Bitwarden Seeder Utility

A CLI wrapper around the Seeder library for generating test data in a Bitwarden database.

**Not sure what to run?** See [Scenarios](../Seeder/Seeds/docs/scenarios/README.md) — problem-oriented guides that map common tasks to commands.

## Getting Started

Build and run from the `util/SeederUtility` directory:

```bash
dotnet build
dotnet run -- <command> [options]
```

**Login Credentials:** All seeded users use password `asdfasdfasdf` by default (override with `--password`). For org presets the owner email is `owner@<domain>` (override with `--owner-email`); for individual presets the email comes from the preset's `user.email` field. For the `individual` command with `--first-name`/`--last-name`, the email is `{first}.{last}@individual.example`; without names, a random Faker identity is generated and mangling is auto-enabled.

## Commands

### `organization` - Seed an Organization

Full control over the org shape via CLI flags — user count, domain, structure, region, density, and plan type. Reach for this when you need flexibility the preset catalog doesn't offer, including orgs with no vault data (every preset includes ciphers).

```bash
# Small org with vault data
dotnet run -- organization -n SmallOrg -d small.example -u 3 -c 10 -g 5 -o Traditional --mangle

# Users only — no vault data
dotnet run -- organization -n MyOrgNoCiphers -u 100 -d myorg-no-ciphers.example

# With custom password and plan type
dotnet run -- organization -n CustomOrg -d custom.example -u 10 -c 100 -g 3 --password "MyTestPassword1" --plan-type teams-annually
```

Additional flags include `--region`, `--kdf-iterations`, and `--plan-type`. Run `dotnet run -- organization --help` for the full list.

Add `--stripe-billing` for an org whose subscription pages actually work — see [Stripe billing](#stripe-billing) below.

### `individual` - Seed an Individual User

Full control over the user via CLI flags — subscription tier, identity, and optional vault data. Reach for this when you need a named user with a predictable email or a personal vault with generated items; the individual presets create bare accounts with no vault data.

```bash
# Named user — predictable email (john.doe@individual.example)
dotnet run -- individual --subscription free --first-name John --last-name Doe

# Premium named user with personal vault (~75 ciphers, 5 folders)
dotnet run -- individual --subscription premium --first-name Jane --last-name Smith --vault

# Random name — mangling auto-enabled
dotnet run -- individual --subscription premium --vault

# Self-hosted instance — signs and writes a license file so premium status is recognized
# (requires a trusted licensing certificate; see the note below)
dotnet run -- individual --subscription premium --first-name Jane --last-name Smith --self-hosted

# Aged account — CreationDate backdated 365 days
dotnet run -- individual --subscription free --account-age-days 365
```

Add `--self-hosted` when targeting a self-hosted instance; without it, premium status won't be recognized. A license is written only when `licenseCertificatePath` and `licenseCertificatePassword` point at a PFX holding the Bitwarden **development** licensing key; the production certificate is deliberately not trusted. With no matching certificate, the seeder logs a warning and skips license generation, so the account is still created, just without recognized premium.

Use `--account-age-days N` to backdate the account's `CreationDate` by `N` days (default `0` = today) for scenarios that depend on account age. Only `CreationDate` is backdated; the revision dates stay at the seed time.

Add `--stripe-billing` to `--subscription premium` for a premium user with a real Stripe subscription — see [Stripe billing](#stripe-billing) below.

### `preset` - Fixture-Based Seeding

Loads a named configuration from the embedded catalog. Presets are curated JSON fixtures with specific users, groups, collections, and cipher relationships — the same data every time. Reach for this when you need a known, reproducible scenario rather than generated data.

```bash
# List available presets
dotnet run -- preset --list

# Day-to-day dev preset with memorable role logins
dotnet run -- preset --name dev.playground

# QA preset with known users and relationships
dotnet run -- preset --name qa.enterprise-basic --mangle

# Scale preset for performance testing
dotnet run -- preset --name scale.md-balanced-sterling-cooper --mangle

# Individual user preset
dotnet run -- preset --name individual.premium --mangle
```

Org presets accept `--mangle` (per-run unique IDs, emails, and identifiers, so you can seed the same preset repeatedly), `--org-name` (override the org display name), and `--owner-email` (override the owner login email). Both overrides compose with `--mangle`. Org presets also accept `--stripe-billing` — see below.

For the full preset catalog, see [presets.md](../Seeder/Seeds/docs/presets.md).

## Stripe billing

Without it, **Billing → Subscription** hangs on a seeded org — nothing real to test seat auto-scaling or upgrades against. By default a seeded organization has no billing at all: its gateway columns stay NULL and the seeder makes **zero** Stripe calls. Pass `--stripe-billing` (on `organization` or `preset`) to create a real customer and subscription in Stripe's test environment, so seat auto-scaling, subscription management, and upgrade flows behave the way they do on a manually created org.

A seeded premium user is the same story: `Premium = 1` and an expiry date, but no Stripe subscription for the premium billing pages to read or change. Pass `--stripe-billing` on `individual --subscription premium` to give the user a real customer and an active premium subscription.

| Flag               | Applies to                                     | Effect                                                                                                                                                                                        |
| ------------------ | ---------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `--stripe-billing` | `organization`, `preset`, `individual`         | Creates a Stripe customer + subscription for the org, or for the premium user. Not valid with `--plan-type free`, on individual presets, with `--subscription free`, or with `--self-hosted`. |
| `--skip-trial`     | `--stripe-billing` on `organization`, `preset` | Subscription starts `active` instead of `trialing`, charging the test card immediately.                                                                                                       |
| `--trial-days N`   | `--stripe-billing` on `organization`, `preset` | Trial length in days, 1–30 (default 30). Mutually exclusive with `--skip-trial`.                                                                                                              |
| `--stripe-card`    | `--stripe-billing` on all three                | Test card the customer pays with (default `visa`). See [Cards](#cards).                                                                                                                       |
| `--stripe-address` | `--stripe-billing` on all three                | Billing address and any tax ID (default `us`). See [Addresses](#addresses).                                                                                                                   |

```bash
# Teams org with a working subscription page, 30-day trial
dotnet run -- organization -n "Billing Test" -u 3 -d billingtest.example --plan-type teams-monthly --stripe-billing --mangle

# Preset org, already-paying subscription (no trial)
dotnet run -- preset --name qa.enterprise-basic --stripe-billing --skip-trial --mangle

# Premium user with an active subscription, charged to the test card
dotnet run -- individual --subscription premium --stripe-billing --mangle
```

Premium has no trial in production, so it has no trial flags: the subscription starts `active` and one test invoice is charged to the test card. Without `--mangle`, `individual --stripe-billing` refuses an email that already exists, before any Stripe call — billing runs before the commit, so a duplicate would otherwise bill a customer and then fail.

On success the output gains two rows:

```bash
StripeCustomer : cus_…
StripeSubscription : sub_…
```

### Cards

`--stripe-card` picks the Stripe test card. The subscription's outcome is the card's outcome, whatever the address.

| Name                      | Stripe token                     | Card number         | Outcome                                                                                                                                  |
| ------------------------- | -------------------------------- | ------------------- | ---------------------------------------------------------------------------------------------------------------------------------------- |
| `visa` (default)          | `pm_card_visa`                   | 4242 4242 4242 4242 | Every charge succeeds.                                                                                                                   |
| `mastercard`              | `pm_card_mastercard`             | 5555 5555 5555 4444 | Every charge succeeds.                                                                                                                   |
| `amex`                    | `pm_card_amex`                   | 3714 496353 98431   | Every charge succeeds.                                                                                                                   |
| `fails-on-charge`         | `pm_card_chargeCustomerFail`     | 4000 0000 0000 0341 | Attaches, then every charge fails. Premium, or an org with `--skip-trial`, comes back `incomplete`; a trial charges nothing at creation. |
| `requires-authentication` | `pm_card_authenticationRequired` | 4000 0027 6000 3184 | Stripe requires 3D Secure authentication on every payment.                                                                               |

Cards that simulate issuer declines aren't offered: Stripe refuses to attach them to a customer, so customer creation fails before anything is seeded.

### Addresses

`--stripe-address` picks the billing address. The tax charged depends on the shared Stripe test account's tax registrations, not on the address alone.

| Name           | Country | Postal code | Tax ID                             |
| -------------- | ------- | ----------- | ---------------------------------- |
| `us` (default) | US      | 43432       | —                                  |
| `us-alaska`    | US      | 99901       | —                                  |
| `de`           | DE      | 10117       | —                                  |
| `de-vat`       | DE      | 10117       | `DE123456789` (organizations only) |
| `ph`           | PH      | 1640        | —                                  |

A personal account takes no tax ID, so `individual --stripe-address de-vat` fails before any write or Stripe call.

Neither flag changes quantities: an organization is billed for the seats, storage, and Secrets Manager seats it was created with, and a premium user is always one seat.

```bash
# Teams org billed in Germany with a VAT ID, paying by Mastercard
dotnet run -- organization -n "Billing VAT" -u 1 -d billingvat.example --plan-type teams-monthly --stripe-billing --stripe-card mastercard --stripe-address de-vat --mangle

# Premium user whose first charge fails, so the web vault shows "Update payment"
dotnet run -- individual --subscription premium --stripe-billing --stripe-card fails-on-charge --mangle
```

### Prerequisites

1. A **test-mode** Stripe secret key (`sk_test_…`) at `globalSettings:stripe:apiKey` in the `bitwarden-seeder-utility` user secrets. A live key is rejected outright — this tool never touches live billing.
2. `globalSettings:pricingUri` must be set, so plan pricing can be resolved. `appsettings.Development.json` ships a value, which means **`ASPNETCORE_ENVIRONMENT=Development` is required**.
3. `globalSettings:selfHosted` must be `false` — the Pricing Service is never called in self-hosted mode, so no plan could be resolved.

All three are checked **before any entity is created**, so a misconfigured opt-in fails with a message and exit code 1 rather than leaving a half-seeded org or user behind.

### Caveats

- Seeded Teams and Enterprise orgs carry `UseSecretsManager = 1` with `SmSeats` NULL. Secrets Manager is included in the subscription only when `SmSeats` has a value, so those orgs have the flag set in the database but no Secrets Manager line items in Stripe.
- Organization billing runs **after** the database commit. If Stripe rejects the request at that point (expired key, network failure), the organization is already committed and the command exits non-zero with the Stripe error and whatever gateway IDs made it through — a failure before customer creation leaves both NULL, but a failure during subscription creation leaves a real `GatewayCustomerId` behind. Check the error message for the actual state; cancel any orphaned Stripe customer before re-seeding.
- Premium billing runs **before** the database commit, as the last step, so the user row is written once with its gateway IDs. If Stripe rejects the request, nothing is committed, and the error names any Stripe customer already created. If the commit fails after billing succeeded, the Stripe customer and subscription exist with no user row: find the customer in the Stripe test dashboard by the seeded email and cancel it.
- An organization's **Billing → Subscription** page renders an `incomplete` subscription (e.g. `fails-on-charge` with `--skip-trial`) only with the `pm-36631-preview-driven-cart` flag on; without it the page is blank.
- Destroying the local database does not cancel the Stripe-side subscriptions. Test-mode subscriptions auto-cancel after roughly 90 days.
