# Bitwarden Seeder Utility

A CLI wrapper around the Seeder library for generating test data in a Bitwarden database.

**Not sure what to run?** See [Scenarios](../Seeder/Seeds/docs/scenarios/README.md) — problem-oriented guides that map common tasks to commands.

## Getting Started

Build and run from the `util/SeederUtility` directory:

```bash
dotnet build
dotnet run -- <command> [options]
```

**Login Credentials:** All seeded users use password `asdfasdfasdf` by default (override with `--password`). For org presets the owner email is `owner@<domain>`; for individual presets the email comes from the preset's `user.email` field. For the `individual` command with `--first-name`/`--last-name`, the email is `{first}.{last}@individual.example`; without names, a random Faker identity is generated and mangling is auto-enabled.

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

### `individual` - Seed an Individual User

Full control over the user via CLI flags — subscription tier, identity, and optional vault data. Reach for this when you need a named user with a predictable email or a personal vault with generated items; the individual presets create bare accounts with no vault data.

```bash
# Named user — predictable email (john.doe@individual.example)
dotnet run -- individual --subscription free --first-name John --last-name Doe

# Premium named user with personal vault (~75 ciphers, 5 folders)
dotnet run -- individual --subscription premium --first-name Jane --last-name Smith --vault

# Random name — mangling auto-enabled
dotnet run -- individual --subscription premium --vault
```

### `preset` - Fixture-Based Seeding

Loads a named configuration from the embedded catalog. Presets are curated JSON fixtures with specific users, groups, collections, and cipher relationships — the same data every time. Reach for this when you need a known, reproducible scenario rather than generated data.

```bash
# List available presets
dotnet run -- preset --list

# QA preset with known users and relationships
dotnet run -- preset --name qa.enterprise-basic --mangle

# Scale preset for performance testing
dotnet run -- preset --name scale.md-balanced-sterling-cooper --mangle

# Individual user preset
dotnet run -- preset --name individual.premium --mangle
```

For the full preset catalog, see [presets.md](../Seeder/Seeds/docs/presets.md).
