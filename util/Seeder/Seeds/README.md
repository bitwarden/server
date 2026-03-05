# Seeds

Hand-crafted JSON fixtures and preset configurations for Bitwarden Seeder test data.

## Quick Start

1. Pick a preset from the catalog below
2. Run: `dotnet run -- seed --preset {name} --mangle`
3. Build to verify: `dotnet build util/Seeder/Seeder.csproj`

## Presets

Presets wire everything together: org + roster + ciphers. Organized by purpose:

| Folder | Purpose | CLI prefix | Example |
|--------|---------|------------|---------|
| `features/` | Test specific Bitwarden features (SSO, TDE, policies) | `features.` | `--preset features.sso-enterprise` |
| `qa/` | Handcrafted fixture data for visual UI verification | `qa.` | `--preset qa.enterprise-basic` |
| `scale/` | Production-calibrated density presets for performance testing | `scale.` | `--preset scale.md-balanced-sterling-cooper` |
| `validation/` | Algorithm verification for seeder development | `validation.` | `--preset validation.density-modeling-power-law-test` |

For the full preset catalog with per-preset details, see [docs/presets.md](docs/presets.md).

For verification queries used during density development, see [docs/verification.md](docs/verification.md).

## Writing Fixtures

For how to create new organization, roster, and cipher fixtures, see [docs/fixtures.md](docs/fixtures.md).

## QA Migration

Mapping from legacy QA test fixtures to seeder presets:

| Legacy Source | Seeder Preset |
|--------------|---------------|
| `CollectionPermissionsOrg.json` | `qa.collection-permissions-enterprise` |
| `EnterpriseOrg.json` | `qa.enterprise-basic` |
| `SsoOrg.json` | `features.sso-enterprise` |
| `TDEOrg.json` | `features.tde-enterprise` |
| Policy Org (Confluence) | `features.policy-enterprise` |
| `FamiliesOrg.json` | `qa.families-basic` |

**Planned:** `qa.free-personal-vault`, `qa.premium-personal-vault`, `features.secrets-manager-enterprise`, `qa.free-org-basic`
