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

**Example** (`fixtures/ciphers/banking-logins.json`):

```json
{
  "$schema": "../../schemas/cipher.schema.json",
  "items": [
    {
      "type": "login",
      "name": "Chase Bank",
      "login": {
        "username": "myuser",
        "password": "MyP@ssw0rd",
        "uris": [{ "uri": "https://chase.com", "match": "domain" }]
      }
    }
  ]
}
```

### Organizations

Organization definitions with name, domain, and seat count.

```json
{
  "$schema": "../../schemas/organization.schema.json",
  "name": "Acme Corp",
  "domain": "acme.com",
  "seats": 100
}
```

### Rosters

Complete user/group/collection structures with permissions. User emails auto-generated as `firstName.lastName@domain`.

**User roles**: `owner`, `admin`, `user`, `custom`

**Collection permissions**: `readOnly`, `hidePasswords`, `manage`

See `rosters/dunder-mifflin.json` for a complete 58-user example.

### Presets

Combine organization, roster, and ciphers into complete scenarios.

**From fixtures**:

```json
{
  "$schema": "../../schemas/preset.schema.json",
  "organization": { "fixture": "acme-corp" },
  "roster": { "fixture": "acme-roster" },
  "ciphers": { "fixture": "banking-logins" }
}
```

**Mixed approach**:

```json
{
  "organization": { "fixture": "acme-corp" },
  "users": { "count": 50 },
  "ciphers": { "count": 500 }
}
```

## Validation

Modern editors validate against `$schema` automatically - errors appear as red squiggles.

Build errors catch schema violations:

```bash
dotnet build util/Seeder/Seeder.csproj
```

## Testing

Add integration test in `test/SeederApi.IntegrationTest/SeedReaderTests.cs`:

```csharp
[Fact]
public void Read_YourFixture_Success()
{
    var result = _reader.Read<SeedFile>("ciphers.your-fixture");
    Assert.NotEmpty(result.Items);
}
```

## Naming Conventions

| Element     | Pattern            | Example                  |
| ----------- | ------------------ | ------------------------ |
| File names  | kebab-case         | `banking-logins.json`    |
| Item names  | Title case, unique | `Chase Bank Login`       |
| User refs   | firstName.lastName | `jane.doe`               |
| Org domains | Realistic or .test | `acme.com`, `test.local` |

## Security

- Test password: `asdfasdfasdf`
- Use fictional names/addresses
- Never commit real passwords or PII
- Never seed production databases

## Examples

- **Small org**: `presets/dunder-mifflin-full.json` (58 users, realistic structure)
- **Browser testing**: `ciphers/autofill-testing.json` (18 specialized items)
- **Real websites**: `ciphers/public-site-logins.json` (90+ website examples)
