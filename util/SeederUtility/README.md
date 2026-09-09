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

| Flag               | Applies to               | Effect                                                                                                            |
| ------------------ | ------------------------ | ----------------------------------------------------------------------------------------------------------------- |
| `--stripe-billing` | `organization`, `preset` | Creates a Stripe customer + subscription for the org. Not valid with `--plan-type free` or on individual presets. |
| `--skip-trial`     | with `--stripe-billing`  | Subscription starts `active` instead of `trialing`, charging the `pm_card_visa` test card immediately.            |
| `--trial-days N`   | with `--stripe-billing`  | Trial length in days, 1–30 (default 30). Mutually exclusive with `--skip-trial`.                                  |

```bash
# Teams org with a working subscription page, 30-day trial
dotnet run -- organization -n "Billing Test" -u 3 -d billingtest.example --plan-type teams-monthly --stripe-billing --mangle

# Preset org, already-paying subscription (no trial)
dotnet run -- preset --name qa.enterprise-basic --stripe-billing --skip-trial --mangle
```

On success the output gains two rows:

```bash
StripeCustomer : cus_…
StripeSubscription : sub_…
```

### Prerequisites

1. A **test-mode** Stripe secret key (`sk_test_…`) at `globalSettings:stripe:apiKey` in the `bitwarden-seeder-utility` user secrets. A live key is rejected outright — this tool never touches live billing.
2. `globalSettings:pricingUri` must be set, so plan pricing can be resolved. `appsettings.Development.json` ships a value, which means **`ASPNETCORE_ENVIRONMENT=Development` is required**.
3. `globalSettings:selfHosted` must be `false` — the Pricing Service is never called in self-hosted mode, so no plan could be resolved.

All three are checked **before any entity is created**, so a misconfigured opt-in fails with a message and exit code 1 rather than leaving a half-seeded org behind.

### Caveats

- Seeded Teams and Enterprise orgs carry `UseSecretsManager = 1` with `SmSeats` NULL. Secrets Manager is included in the subscription only when `SmSeats` has a value, so those orgs have the flag set in the database but no Secrets Manager line items in Stripe.
- Billing runs **after** the database commit. If Stripe rejects the request at that point (expired key, network failure), the organization is already committed and the command exits non-zero with the Stripe error and whatever gateway IDs made it through — a failure before customer creation leaves both NULL, but a failure during subscription creation leaves a real `GatewayCustomerId` behind. Check the error message for the actual state; cancel any orphaned Stripe customer before re-seeding.
- Destroying the local database does not cancel the Stripe-side subscriptions. Test-mode subscriptions auto-cancel after roughly 90 days.
