# Seeds

Hand-crafted JSON fixtures for Bitwarden Seeder test data.

## Quick Start

1. Create a JSON file in the right `fixtures/` subfolder
2. Add the `$schema` line — your editor picks up validation automatically
3. Build to verify: `dotnet build util/Seeder/Seeder.csproj`

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

### Presets

Presets **wire everything together**: org + roster + ciphers. You can reference fixtures by name or generate data with counts.

Three styles:

- **Fixture-based**: `enterprise-basic.json` — references org, roster, and cipher fixtures
- **Generated**: `wonka-teams-small.json` — uses `count` parameters to create users, groups, collections, ciphers
- **Feature-specific**: `tde-enterprise.json`, `policy-enterprise.json` — adds SSO config, policies

Presets can also define inline orgs (name + domain right in the preset) instead of referencing a fixture — see `large-enterprise.json`.

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

## QA Test Fixture Migration Matrix

These Seeds consolidate test data previously found across the `bitwarden/test` repo.
The table below maps existing QA fixtures to their Seeder equivalents.

| QA Source (`test/Bitwarden.Web.Tests/TestData/SetupData/`) | Used By                           | Seeder Preset                       | Org Fixture         | Roster Fixture           | Cipher Fixture           |
| ---------------------------------------------------------- | --------------------------------- | ----------------------------------- | ------------------- | ------------------------ | ------------------------ |
| `CollectionPermissionsOrg.json`                            | Web, Extension                    | `collection-permissions-enterprise` | `cobalt-logistics`  | `collection-permissions` | `collection-permissions` |
| `EnterpriseOrg.json`                                       | Web, Extension, Android, iOS, CLI | `enterprise-basic`                  | `redwood-analytics` | `enterprise-basic`       | `enterprise-basic`       |
| `SsoOrg.json`                                              | Web                               | `sso-enterprise`                    | `verdant-health`    | `starter-team`           | `sso-vault`              |
| `TDEOrg.json`                                              | Web, Extension, Android, iOS      | `tde-enterprise`                    | `obsidian-labs`     | `starter-team`           | `tde-vault`              |
| _(Confluence: Policy Org guide)_                           | QA manual setup                   | `policy-enterprise`                 | `pinnacle-designs`  | `starter-team`           | —                        |
| `FamiliesOrg.json`                                         | Web, Extension                    | `families-basic`                    | `adams-family`      | `family`                 | —                        |

### Not Yet Migrated

| QA Source             | Used By                      | Status                                                                |
| --------------------- | ---------------------------- | --------------------------------------------------------------------- |
| `FreeAccount.json`    | All 7 platforms              | Planned — `free-personal-vault` preset (separate PR due to file size) |
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
