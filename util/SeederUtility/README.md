# Bitwarden Seeder Utility

A CLI wrapper around the Seeder library for generating test data in a Bitwarden database.

## Getting Started

Build and run from the `util/SeederUtility` directory:

```bash
dotnet build
dotnet run -- <command> [options]
```

**Login Credentials:** All seeded users use password `asdfasdfasdf` by default (override with `--password`). The owner email is `owner@<domain>`.

## Commands

### `organization` - Users Only (No Vault Data)

```bash
# 100 users
dotnet run -- organization -n MyOrgNoCiphers -u 100 -d myorg-no-ciphers.example

# 10,000 users for load testing
dotnet run -- organization -n LargeOrgNoCiphers -u 10000 -d large-org-no-ciphers.example
```

### `seed` - Fixture-Based Seeding

```bash
# List available presets and fixtures
dotnet run -- seed --list

# Load the Dunder Mifflin preset (58 users, 14 groups, 15 collections, ciphers)
dotnet run -- seed --preset dunder-mifflin-enterprise-full

# Load with ID mangling for test isolation
dotnet run -- seed --preset dunder-mifflin-enterprise-full --mangle

dotnet run -- seed --preset stark-free-basic --mangle

# Large enterprise preset for performance testing
dotnet run -- seed --preset large-enterprise

dotnet run -- seed --preset dunder-mifflin-enterprise-full --password "MyTestPassword1" --mangle
```

### `vault-organization` - Users + Encrypted Vault Data

```bash
# Tiny org — quick sanity check
dotnet run -- vault-organization -n SmallOrg -d small.example -u 3 -c 10 -g 5 -o Traditional -m

# Mid-size Traditional org with realistic status mix
dotnet run -- vault-organization -n MidOrg -d mid.example -u 50 -c 1000 -g 15 -o Traditional -m

# Mid-size with dense cipher-to-user ratio
dotnet run -- vault-organization -n DenseOrg -d dense.example -u 75 -c 650 -g 20 -o Traditional -m

# Large Modern org
dotnet run -- vault-organization -n LargeOrg -d large.example -u 500 -c 10000 -g 85 -o Modern -m

# Stress test — massive Spotify-style org
dotnet run -- vault-organization -n StressOrg -d stress.example -u 8000 -c 100000 -g 125 -o Spotify -m

# Regional data variants
dotnet run -- vault-organization -n EuropeOrg -d europe.example -u 10 -c 100 -g 5 --region Europe
dotnet run -- vault-organization -n ApacOrg -d apac.example -u 17 -c 600 -g 12 --region AsiaPacific

# With ID mangling for test isolation (prevents collisions with existing data)
dotnet run -- vault-organization -n IsolatedOrg -d isolated.example -u 5 -c 25 -g 4 -o Spotify --mangle

# With custom password for all accounts
dotnet run -- vault-organization -n CustomPwOrg -d custom-password-05.example -u 10 -c 100 -g 3 --password "MyTestPassword1" --plan-type teams-annually

# Free plan org (limited to 2 seats, 2 collections)
dotnet run -- vault-organization -n FreeOrg -d free.example -u 1 -c 10 -g 1 --plan-type free

# Teams plan org
dotnet run -- vault-organization -n TeamsOrg -d teams.example -u 20 -c 200 -g 5 --plan-type teams-annually
```
