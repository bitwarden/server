# Bitwarden Database Seeder Utility

A CLI wrapper around the Seeder library for generating test data in your local Bitwarden database.

## Getting Started

Build and run from the `util/DbSeederUtility` directory:

```bash
dotnet build
dotnet run -- <command> [options]
```

**Login Credentials:** All seeded users use password `asdfasdfasdf` by default (override with `--password`). The owner email is `owner@<domain>`.

## Commands

### `seed` - Fixture-Based Seeding

```bash
# List available presets and fixtures
dotnet run -- seed --list

# Load the Dunder Mifflin preset (58 users, 14 groups, 15 collections, ciphers)
dotnet run -- seed --preset dunder-mifflin-full

# Load with ID mangling for test isolation
dotnet run -- seed --preset dunder-mifflin-full --mangle

# Large enterprise preset for performance testing
dotnet run -- seed --preset large-enterprise

dotnet run -- seed --preset dunder-mifflin-full --password "MyTestPassword1" --mangle
```

### `organization` - Users Only (No Vault Data)

```bash
# 100 users
dotnet run -- organization -n MyOrg -u 100 -d myorg.com

# 10,000 users for load testing
dotnet run -- organization -n seeded -u 10000 -d large.test
```

### `vault-organization` - Users + Encrypted Vault Data

```bash
# Tiny org — quick sanity check
dotnet run -- vault-organization -n SmallOrg -d small.test -u 3 -c 10 -g 5 -o Traditional -m

# Mid-size Traditional org with realistic status mix
dotnet run -- vault-organization -n MidOrg -d mid.test -u 50 -c 1000 -g 15 -o Traditional -m

# Mid-size with dense cipher-to-user ratio
dotnet run -- vault-organization -n DenseOrg -d dense.test -u 75 -c 650 -g 20 -o Traditional -m

# Large Modern org
dotnet run -- vault-organization -n LargeOrg -d large.test -u 500 -c 10000 -g 85 -o Modern -m

# Stress test — massive Spotify-style org
dotnet run -- vault-organization -n StressOrg -d stress.test -u 8000 -c 100000 -g 125 -o Spotify -m

# Regional data variants
dotnet run -- vault-organization -n EuropeOrg -d europe.test -u 10 -c 100 -g 5 --region Europe
dotnet run -- vault-organization -n ApacOrg -d apac.test -u 17 -c 600 -g 12 --region AsiaPacific

# With ID mangling for test isolation (prevents collisions with existing data)
dotnet run -- vault-organization -n IsolatedOrg -d isolated.test -u 5 -c 25 -g 4 -o Spotify --mangle

# With custom password for all accounts
dotnet run -- vault-organization -n CustomPwOrg -d custom-password-02.test -u 10 -c 100 -g 3 --password "MyTestPassword1"

# Free plan org (limited to 2 seats, 2 collections)
dotnet run -- vault-organization -n FreeOrg -d free.test -u 1 -c 10 -g 1 --plan-type free

# Teams plan org
dotnet run -- vault-organization -n TeamsOrg -d teams.test -u 20 -c 200 -g 5 --plan-type teams-annually
```
