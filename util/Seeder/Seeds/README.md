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
│   ├── ciphers/        Vault items (logins, cards, identities, notes)
│   ├── organizations/  Organization definitions
│   ├── rosters/        Users, groups, collections, permissions
│   └── presets/        Complete seeding scenarios
├── schemas/            JSON Schema validation (auto-checked by editors)
├── templates/          Starter files - copy these
│   └── CONTRIBUTING.md Detailed guide for contributors
└── README.md           This file
```

## Fixtures Overview

### Ciphers

Vault items - logins, cards, identities, secure notes.

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

## Security

- Test password: See `UserSeeder.DefaultPassword` constant
- Use fictional names/addresses
- Never commit real passwords or PII
- Never seed production databases
