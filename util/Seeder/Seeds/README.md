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
| `validation/` | Algorithm verification for seeder development | `validation.` | `--preset validation.density-modeling-power-law-test` |

## Writing Fixtures

### Organizations

Just a name and domain. That's it.
Domains must use `.example` (RFC 2606 — guaranteed unresolvable, safe for QA email pipelines).
Plan type and seats are defined in presets, not here.

See: `fixtures/organizations/redwood-analytics.json`

### Rosters

Users, groups, and collections for an org.

- Users have a `firstName`, `lastName`, and `role` (`owner`, `admin`, `user`, `custom`)
- The Seeder pipeline builds emails as `firstName.lastName@domain`, so `"Family"` + `"Mom"` at domain `acme.example` becomes `family.mom@acme.example` or `a1b2c3d4+family.mom@acme.example` with mangling on
- Groups reference users by that same email prefix (e.g. `"family.mom"`)
- Collections assign permissions to groups or individual users (`readOnly`, `hidePasswords`, `manage` — all default false)

See: `starter-team.json` (minimal), `family.json` (groups + collections), `dunder-mifflin.json` (58-user enterprise)

### Ciphers

Vault items. Each item needs a `type` and `name`.

| Type         | Required Object | Description                |
| ------------ | --------------- | -------------------------- |
| `login`      | `login`         | Website credentials + URIs |
| `card`       | `card`          | Payment card details       |
| `identity`   | `identity`      | Personal identity info     |
| `secureNote` | —               | Uses `notes` field only    |
| `sshKey`     | `sshKey`        | SSH key credentials        |

See: `fixtures/ciphers/enterprise-basic.json`

## Naming Conventions

| Element     | Pattern            | Example               |
| ----------- | ------------------ | --------------------- |
| File names  | kebab-case         | `banking-logins.json` |
| Item names  | Title case, unique | `Chase Bank Login`    |
| User refs   | firstName.lastName | `jane.doe`            |
| Org domains | .example           | `acme.example`        |

## Validation

Your editor validates against `$schema` automatically — errors show up as red squiggles. Build also catches schema violations:

```bash
dotnet build util/Seeder/Seeder.csproj
```

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

## Security

- Use fictional names/addresses
- Never commit real passwords or PII
- Never seed production databases
