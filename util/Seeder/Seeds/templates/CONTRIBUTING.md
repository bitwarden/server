# Contributing to Bitwarden Seeder Fixtures

This guide explains how to create new seed fixtures for the Bitwarden Seeder.

## Quick Start

1. Copy a template from this folder
2. Rename it and place it in the appropriate `fixtures/` subfolder
3. Edit the JSON (your editor validates against the `$schema` automatically)
4. Build to verify: `dotnet build util/Seeder/Seeder.csproj`
5. Test your fixture (see Testing section below)

## File Structure

```
Seeds/
├── fixtures/              Your seed data goes here
│   ├── ciphers/          Vault items (logins, cards, identities, notes)
│   ├── organizations/    Organization definitions
│   ├── rosters/          Users, groups, and collections
│   └── presets/          Complete seeding scenarios
├── schemas/              JSON Schema validation (don't edit)
├── templates/            Copy these to get started (you are here!)
└── README.md             Overview documentation
```

## Creating Fixtures

### 1. Cipher Fixtures

**Template:** `cipher.template.json`
**Location:** `fixtures/ciphers/your-name.json`

Cipher fixtures contain vault items. Each item has a `type` that determines its structure:

| Type         | Required Object | Use Case                    |
|-------------|----------------|-----------------------------|
| `login`      | `login`        | Website credentials         |
| `card`       | `card`         | Payment cards               |
| `identity`   | `identity`     | Personal identity info      |
| `secureNote` | —              | Notes (uses `notes` field)  |

**Example:**
```json
{
  "$schema": "../schemas/cipher.schema.json",
  "items": [
    {
      "type": "login",
      "name": "GitHub",
      "login": {
        "username": "myuser",
        "password": "MyP@ssw0rd",
        "uris": [
          {
            "uri": "https://github.com/login",
            "match": "domain"
          }
        ]
      }
    }
  ]
}
```

**Naming Rules:**
- Use kebab-case: `banking-logins.json`, `development-tools.json`
- Be descriptive: fixture name should indicate purpose
- Item names must be unique within each file

**Custom Fields:**
```json
{
  "type": "login",
  "name": "Example with Custom Fields",
  "login": { ... },
  "fields": [
    {
      "name": "Security Question",
      "value": "What is your favorite color?",
      "type": "text"
    },
    {
      "name": "API Key",
      "value": "sk_test_123456",
      "type": "hidden"
    }
  ]
}
```

### 2. Organization Fixtures

**Template:** `organization.template.json`
**Location:** `fixtures/organizations/your-name.json`

Organization fixtures define a single organization.

**Example:**
```json
{
  "$schema": "../schemas/organization.schema.json",
  "name": "Dunder Mifflin Paper Company",
  "domain": "dundermifflin.com",
  "seats": 100
}
```

**Fields:**
- `name`: Display name of the organization
- `domain`: Used for email addresses (e.g., `user@dundermifflin.com`)
- `seats`: Number of user slots (default: 10)

### 3. Roster Fixtures

**Template:** `roster.template.json`
**Location:** `fixtures/rosters/your-name.json`

Roster fixtures define the people structure: users, groups, and collections with permissions.

**Email Generation:**
Email addresses are automatically generated as `firstName.lastName@domain` where domain comes from the organization.

**Example:**
```json
{
  "$schema": "../schemas/roster.schema.json",
  "users": [
    {
      "firstName": "Jane",
      "lastName": "Doe",
      "title": "CEO",
      "role": "owner",
      "branch": "Headquarters",
      "department": "Executive"
    }
  ],
  "groups": [
    {
      "name": "Executives",
      "members": ["jane.doe"]
    }
  ],
  "collections": [
    {
      "name": "Company Secrets",
      "groups": [
        {
          "group": "Executives",
          "readOnly": false,
          "hidePasswords": false,
          "manage": true
        }
      ]
    }
  ]
}
```

**User Roles:**
- `owner`: Full control
- `admin`: Manage users, groups, collections
- `user`: Regular member (default)
- `custom`: Custom permissions

**Collection Permissions:**
- `readOnly`: Can view but not edit items
- `hidePasswords`: Can view items but passwords are hidden
- `manage`: Can manage collection and its assignments

**Collection Hierarchy:**
Use `/` in collection names for visual nesting:
```json
{
  "name": "Engineering/Frontend"
}
```

### 4. Preset Fixtures

**Template:** `preset.template.json`
**Location:** `fixtures/presets/your-name.json`

Presets compose organization, roster, and ciphers into complete seeding scenarios.

**Example:**
```json
{
  "$schema": "../schemas/preset.schema.json",
  "organization": {
    "fixture": "dunder-mifflin"
  },
  "roster": {
    "fixture": "dunder-mifflin"
  },
  "ciphers": {
    "fixture": "autofill-testing"
  }
}
```

**Inline Organization:**
```json
{
  "organization": {
    "name": "Quick Test Org",
    "domain": "test.example.com",
    "seats": 20
  },
  "users": {
    "count": 15,
    "realisticStatusMix": true
  },
  "groups": {
    "count": 5
  },
  "collections": {
    "count": 10
  },
  "ciphers": {
    "count": 100
  }
}
```

## Validation

### Automatic Schema Validation

Modern editors (VS Code, Visual Studio, JetBrains IDEs) automatically validate JSON files against the `$schema` reference.

**VS Code Setup:**
1. Open any `.json` file in the `fixtures/` folder
2. The `$schema` property enables IntelliSense and validation
3. Errors appear as red squiggles

### Manual Validation

Build the Seeder project to validate all embedded JSON files:

```bash
dotnet build util/Seeder/Seeder.csproj
```

Any schema violations will cause build errors.

## Testing Your Fixture

### Integration Tests

Create a test in `test/SeederApi.IntegrationTest/SeedReaderTests.cs`:

```csharp
[Fact]
public void ReadCipherSeed_YourFixture_Success()
{
    var result = _seedReader.ReadCipherSeed("your-fixture-name");

    Assert.NotNull(result);
    Assert.NotEmpty(result.Items);
    // Add specific assertions for your fixture
}
```

### Manual Testing

Use the SeederApi to test your fixture:

```bash
# Start the SeederApi
dotnet run --project util/SeederApi

# POST to /seed endpoint with your preset
curl -X POST http://localhost:5000/seed \
  -H "Content-Type: application/json" \
  -d '{"preset": "your-preset-name"}'
```

## Naming Conventions

### File Names
- **Use kebab-case:** `my-fixture-name.json`
- **Be descriptive:** Name should indicate purpose
- **Avoid versions:** Don't use `v1`, `v2` suffixes

### Item Names (Ciphers)
- **Must be unique** within each cipher file
- **Be specific:** "GitHub Login" not "Login 1"
- **Use title case:** "My Bank Account" not "my bank account"

### Organization Domains
- Use `.example.com` for test/demo organizations
- Use realistic domains for specific company fixtures (e.g., `dundermifflin.com`)

### User References
- Use email prefix format: `firstName.lastName`
- Example: `"members": ["jane.doe", "john.smith"]`

## Common Patterns

### Large Organizations

For organizations with many users, consider:
1. Organize by department/branch
2. Use realistic role distribution (mostly `user`, some `admin`, few `owner`)
3. Create logical group structures

See `fixtures/rosters/dunder-mifflin.json` for a complete example with 58 users.

### Test Scenarios

When creating fixtures for specific testing scenarios:
1. Name the fixture after the test purpose: `autofill-testing.json`
2. Include comments in commit messages explaining the test cases
3. Keep items minimal but comprehensive

### Reusable Components

You can mix fixtures and generation in presets:
```json
{
  "organization": {
    "fixture": "my-org"
  },
  "roster": {
    "fixture": "my-roster"
  },
  "ciphers": {
    "count": 500
  }
}
```

## Security Considerations

### Passwords
- Use placeholder passwords like `asdfasdfasdf` (the Seeder test password)
- Never commit real passwords

### Personal Information
- Use fictional names and addresses
- Use `.example.com` domains for email addresses
- Don't use real SSNs, passport numbers, or license numbers

### Sensitive Data
- These fixtures are for testing only
- Never seed production databases with fixture data
- All seeded data is encrypted using test encryption keys

## Getting Help

- **Schema Documentation:** See comments in `schemas/*.json`
- **Examples:** Browse `fixtures/` for real-world examples
- **Seeder README:** `util/Seeder/README.md` for architecture overview
- **Build Issues:** Check that JSON is valid and matches schema

## CI/CD Integration

Fixtures are embedded as resources in the Seeder assembly. The build process:
1. Validates all JSON against schemas
2. Embeds files as `EmbeddedResource`
3. Makes them available via `SeedReader` service

After adding a new fixture:
1. Ensure it's in the correct `fixtures/` subfolder
2. Build the project to verify it's embedded
3. Run tests to validate it loads correctly
