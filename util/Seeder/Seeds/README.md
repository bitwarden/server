# Seeds

Hand-crafted JSON fixtures for Bitwarden Seeder test data.

## Quick Start

1. Copy template from `templates/` to appropriate `fixtures/` subfolder
2. Edit JSON (your editor validates against `$schema` automatically)
3. Build to verify: `dotnet build util/Seeder/Seeder.csproj`

## File Structure

```
Seeds/
├── fixtures/           Your seed data goes here
│   ├── ciphers/        Vault items
│   ├── organizations/  Organization definitions
│   ├── rosters/        Users, groups, collections, permissions
│   └── presets/        Complete seeding scenarios
├── schemas/            JSON Schema validation (auto-checked by editors)
├── templates/          Starter files - copy these
└── README.md           This file
```

## Fixtures Overview

### Ciphers

| Type         | Required Object | Description                |
| ------------ | --------------- | -------------------------- |
| `login`      | `login`         | Website credentials + URIs |
| `card`       | `card`          | Payment card details       |
| `identity`   | `identity`      | Personal identity info     |
| `secureNote` | —               | Uses `notes` field only    |
| `sshKey`     | `sshKey`        | SSH key credentials        |

**Schema**: `schemas/cipher.schema.json`

### Organizations

Organization identity definitions with name and domain. Plan type and seats are defined in presets, not org fixtures.

**Required fields**: `name`, `domain`

**Schema**: `schemas/organization.schema.json`

### Rosters

Complete user/group/collection structures with permissions. User emails auto-generated as `firstName.lastName@domain`.

**User roles**: `owner`, `admin`, `user`, `custom`
**Collection permissions**: `readOnly`, `hidePasswords`, `manage`
**Schema**: `schemas/roster.schema.json`
**Example**: See `fixtures/rosters/dunder-mifflin.json` for a complete 58-user example

### Presets

Combine organization, roster, and ciphers into complete scenarios. Presets can reference fixtures, generate data programmatically, or mix both approaches.

**Key features**:

- Reference existing fixtures by name
- Generate users, groups, collections, and ciphers with count parameters
- Add personal ciphers (user-owned, encrypted with user key, not in collections)
- Mix fixture references and generated data

**Schema**: `schemas/preset.schema.json`
**Examples**: See `fixtures/presets/` for complete examples including fixture-based, generated, and hybrid approaches

## Validation

Modern editors validate against `$schema` automatically - errors appear as red squiggles.

Build errors catch schema violations:

```bash
dotnet build util/Seeder/Seeder.csproj
```

## Naming Conventions

| Element     | Pattern            | Example                  |
| ----------- | ------------------ | ------------------------ |
| File names  | kebab-case         | `banking-logins.json`    |
| Item names  | Title case, unique | `Chase Bank Login`       |
| User refs   | firstName.lastName | `jane.doe`               |
| Org domains | Realistic or .test | `acme.com`, `test.local` |

## QA Test Fixture Migration Matrix

These Seeds consolidate test data previously found across the `bitwarden/test` repo.
The table below maps existing QA fixtures to their Seeder equivalents.

| QA Source (`test/Bitwarden.Web.Tests/TestData/SetupData/`) | Used By                           | Seeder Preset                       | Org Fixture                 | Roster Fixture           | Cipher Fixture           |
| ---------------------------------------------------------- | --------------------------------- | ----------------------------------- | --------------------------- | ------------------------ | ------------------------ |
| `CollectionPermissionsOrg.json`                            | Web, Extension                    | `collection-permissions-enterprise` | `qa-collection-permissions` | `collection-permissions` | `collection-permissions` |
| `EnterpriseOrg.json`                                       | Web, Extension, Android, iOS, CLI | `enterprise-basic`                  | `qa-enterprise`             | `enterprise-basic`       | `enterprise-basic`       |
| `SsoOrg.json`                                              | Web                               | `sso-enterprise`                    | `qa-sso-org`                | `sso-basic`              | `sso-vault`              |
| `TDEOrg.json`                                              | Web, Extension, Android, iOS      | `tde-enterprise`                    | `qa-tde-org`                | `tde-basic`              | `tde-vault`              |
| _(Confluence: Policy Org guide)_                           | QA manual setup                   | `policy-enterprise`                 | `qa-policy-org`             | `policy-org`             | —                        |

### Not Yet Migrated

| QA Source             | Used By                      | Status                                                                |
| --------------------- | ---------------------------- | --------------------------------------------------------------------- |
| `FreeAccount.json`    | All 7 platforms              | Planned — `free-personal-vault` preset (separate PR due to file size) |
| `FamiliesOrg.json`    | Web, Extension               | Planned — `families-basic` preset                                     |
| `PremiumAccount.json` | Web, Extension, Android, iOS | Planned — `premium-personal-vault` preset                             |
| `SecretsManager.json` | Web                          | Planned — `secrets-manager-enterprise` preset                         |
| `FreeOrg.json`        | Web                          | Planned — `free-org-basic` preset                                     |

### Additional Sources

| Source                             | Location                        | Status                                                                                                           |
| ---------------------------------- | ------------------------------- | ---------------------------------------------------------------------------------------------------------------- |
| `bw_importer.py`                   | `github.com/bitwarden/qa-tools` | Superseded by generation-based presets (`"ciphers": {"count": N}`)                                               |
| `mass_org_manager.py`              | `github.com/bitwarden/qa-tools` | Superseded by roster fixtures with groups/members/collections                                                    |
| Admin Console Testing Setup guides | Confluence QA space             | Codified as `collection-permissions-enterprise`, `policy-enterprise`, `sso-enterprise`, `tde-enterprise` presets |

## Security

- Use fictional names/addresses
- Never commit real passwords or PII
- Never seed production databases
