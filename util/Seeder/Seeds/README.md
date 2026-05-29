# Seeds

Hand-crafted JSON fixtures and preset configurations for Bitwarden Seeder test data.

## Quick Start

1. Pick a preset from the catalog below
2. Run: `dotnet run -- preset --name {name} --mangle`
3. Build to verify: `dotnet build util/Seeder/Seeder.csproj`

## How Presets Work

Fixtures (organizations, rosters, ciphers) are independent building blocks that never reference each other. A preset composes them together and defines cross-cutting relationships like folder assignments and favorites. Presets can reference fixture files, generate data inline, or mix both. See [docs/architecture.md](docs/architecture.md) for the full architecture guide.

## Presets

Presets wire everything together. Org presets compose org + roster + ciphers; individual presets compose a single user + vault data. Organized by purpose:

| Folder        | Purpose                                                                     | CLI prefix    | Example                                               |
| ------------- | --------------------------------------------------------------------------- | ------------- | ----------------------------------------------------- |
| `features/`   | Test specific Bitwarden features (SSO, TDE, policies)                       | `features.`   | `--name features.sso-enterprise`                    |
| `qa/`         | Known users, groups, collections, and permissions you can point a client to | `qa.`         | `--name qa.enterprise-basic`                        |
| `scale/`      | Production-calibrated density presets for performance testing               | `scale.`      | `--name scale.md-balanced-sterling-cooper`          |
| `individual/` | Individual user accounts (no organization)                                   | `individual.` | `--name individual.premium`                         |
| `validation/` | Algorithm verification for seeder development                               | `validation.` | `--name validation.density-modeling-power-law-test` |

For the full preset catalog with per-preset details, see [docs/presets.md](docs/presets.md).

For verification queries used during density development, see [docs/verification.md](docs/verification.md).

## Writing Fixtures

For how to create new organization, roster, and cipher fixtures, see [docs/fixtures.md](docs/fixtures.md).
