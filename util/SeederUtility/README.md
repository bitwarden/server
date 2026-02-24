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

### `organization` - Seed an Organization

```bash
# Users only — no vault data
dotnet run -- organization -n MyOrgNoCiphers -u 100 -d myorg-no-ciphers.example

# 10,000 users for load testing
dotnet run -- organization -n LargeOrgNoCiphers -u 10000 -d large-org-no-ciphers.example

# With vault data (ciphers, groups, collections)
dotnet run -- organization -n SmallOrg -d small.example -u 3 -c 10 -g 5 -o Traditional -m

# Mid-size Traditional org with realistic status mix
dotnet run -- organization -n MidOrg -d mid.example -u 50 -c 1000 -g 15 -o Traditional -m

# Large Modern org
dotnet run -- organization -n LargeOrg -d large.example -u 500 -c 10000 -g 85 -o Modern -m

# Stress test — massive Spotify-style org
dotnet run -- organization -n StressOrg -d stress.example -u 8000 -c 100000 -g 125 -o Spotify -m

# Regional data variants
dotnet run -- organization -n EuropeOrg -d europe.example -u 10 -c 100 -g 5 --region Europe
dotnet run -- organization -n ApacOrg -d apac.example -u 17 -c 600 -g 12 --region AsiaPacific

# With ID mangling for test isolation
dotnet run -- organization -n IsolatedOrg -d isolated.example -u 5 -c 25 -g 4 -o Spotify --mangle

# With custom password and plan type
dotnet run -- organization -n CustomPwOrg -d custom-password-05.example -u 10 -c 100 -g 3 --password "MyTestPassword1" --plan-type teams-annually

# Free plan org
dotnet run -- organization -n FreeOrg -d free.example -u 1 -c 10 -g 1 --plan-type free

# Teams plan org
dotnet run -- organization -n TeamsOrg -d teams.example -u 20 -c 200 -g 5 --plan-type teams-annually
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

