# Writing Fixtures

Hand-crafted JSON fixtures for Bitwarden Seeder test data. Add a `$schema` line for editor validation.

## Organizations

Just a name and domain. Domains must use `.example` (RFC 2606 — guaranteed unresolvable, safe for QA email pipelines). Plan type and seats are defined in presets, not here.

See: `fixtures/organizations/redwood-analytics.json`

## Rosters

Users, groups, and collections for an org.

- Users have a `firstName`, `lastName`, and `role` (`owner`, `admin`, `user`, `custom`)
- The Seeder builds emails as `firstName.lastName@domain`, so `"Family"` + `"Mom"` at domain `acme.example` becomes `family.mom@acme.example` or `a1b2c3d4+family.mom@acme.example` with mangling
- Groups reference users by that same email prefix (e.g. `"family.mom"`)
- Collections assign permissions to groups or individual users (`readOnly`, `hidePasswords`, `manage` — all default false)

See: `starter-team.json` (minimal), `family.json` (groups + collections), `dunder-mifflin.json` (58-user enterprise)

## Ciphers

Vault items. Each item needs a `type` and `name`.

| Type | Required Object | Description |
|------|----------------|-------------|
| `login` | `login` | Website credentials + URIs |
| `card` | `card` | Payment card details |
| `identity` | `identity` | Personal identity info |
| `secureNote` | — | Uses `notes` field only |
| `sshKey` | `sshKey` | SSH key credentials |

See: `fixtures/ciphers/enterprise-basic.json`

## Naming Conventions

| Element | Pattern | Example |
|---------|---------|---------|
| File names | kebab-case | `banking-logins.json` |
| Item names | Title case, unique | `Chase Bank Login` |
| User refs | firstName.lastName | `jane.doe` |
| Org domains | .example | `acme.example` |

## Validation

Your editor validates against `$schema` automatically. Build also catches schema violations:

```bash
dotnet build util/Seeder/Seeder.csproj
```

## Security

- Use fictional names/addresses
- Never commit real passwords or PII
- Never seed production databases
