# Seeder Data System

Structured data generation for realistic vault seeding. Designed for extensibility and spec-driven generation.

## Architecture

Foundation layer for all cipher generation—data and patterns that future cipher types build upon.

- **Enums are the API.** Configure via `CompanyType`, `PasswordStrength`, etc. Everything else is internal.
- **Composable by region.** Arrays aggregate with `[.. UsNames, .. EuropeanNames]`. New region = new array + one line change.
- **Deterministic.** Seeded randomness means same org ID → same test data → reproducible debugging.
- **Filterable.** `Companies.Filter(type, region, category)` for targeted data selection.

---

## Current Capabilities

### Login Ciphers

- 50 real companies across 3 regions with metadata (category, type, domain)
- 200 first names + 200 last names (US, European)
- 6 username patterns (corporate email conventions)
- 3 password strength levels (95 total passwords)

### Organizational Structures

- Traditional (departments + sub-units)
- Spotify Model (tribes, squads, chapters, guilds)
- Modern/AI-First (feature teams, platform teams, pods)

---

## Roadmap

### Phase 1: Additional Cipher Types

| Cipher Type | Data Needed                                          | Status      |
| ----------- | ---------------------------------------------------- | ----------- |
| Login       | Companies, Names, Passwords, Patterns                | ✅ Complete |
| Card        | Card networks, bank names, realistic numbers         | ⬜ Planned  |
| Identity    | Full identity profiles (name, address, SSN patterns) | ⬜ Planned  |
| SecureNote  | Note templates, categories, content generators       | ⬜ Planned  |

### Phase 2: Spec-Driven Generation

Import a specification file and generate a complete vault to match:

```yaml
# Example: organization-spec.yaml
organization:
  name: "Acme Corp"
  users: 500

collections:
  structure: spotify # Use Spotify org model

ciphers:
  logins:
    count: 2000
    companies:
      type: enterprise
      region: north_america
    passwords: mixed # Realistic distribution
    username_pattern: first_dot_last

  cards:
    count: 100
    networks: [visa, mastercard, amex]

  identities:
    count: 200
    regions: [us, europe]

  secure_notes:
    count: 300
    categories: [api_keys, licenses, documentation]
```

**Spec Engine Components (Future)**

- `SpecParser` - YAML/JSON spec file parsing
- `SpecValidator` - Schema validation
- `SpecExecutor` - Orchestrates generation from spec
- `ProgressReporter` - Real-time generation progress

### Phase 3: Data Enhancements

| Enhancement             | Description                                          |
| ----------------------- | ---------------------------------------------------- |
| **Additional Regions**  | LatinAmerica, MiddleEast, Africa companies and names |
| **Industry Verticals**  | Healthcare, Finance, Government-specific companies   |
| **Localized Passwords** | Region-specific common passwords                     |
| **Custom Fields**       | Field templates per cipher type                      |
| **TOTP Seeds**          | Realistic 2FA seed generation                        |
| **Attachments**         | File attachment simulation                           |
| **Password History**    | Historical password entries                          |

### Phase 4: Advanced Features

- **Relationship Graphs** - Ciphers that reference each other (SSO relationships)
- **Temporal Data** - Realistic created/modified timestamps over time
- **Access Patterns** - Simulate realistic collection/group membership distributions
- **Breach Simulation** - Mark specific passwords as "exposed" for security testing

---

## Adding New Data

### New Region (e.g., Swedish Names)

```csharp
// In Names.cs - add array
public static readonly string[] SwedishFirstNames = ["Erik", "Lars", "Anna", ...];
public static readonly string[] SwedishLastNames = ["Andersson", "Johansson", ...];

// Update aggregates
public static readonly string[] AllFirstNames = [.. UsFirstNames, .. EuropeanFirstNames, .. SwedishFirstNames];
public static readonly string[] AllLastNames = [.. UsLastNames, .. EuropeanLastNames, .. SwedishLastNames];
```

### New Company Category

```csharp
// In Enums/CompanyCategory.cs
public enum CompanyCategory
{
    // ... existing ...
    Healthcare,    // Add new category
    Government
}

// In Companies.cs - add companies with new category
new("epic.com", "Epic Systems", CompanyCategory.Healthcare, CompanyType.Enterprise, GeographicRegion.NorthAmerica),
```

### New Password Pattern

```csharp
// In Passwords.cs - add to appropriate strength array
// Strong array - add new passphrase style
"correct-horse-battery-staple",  // Diceware
"Brave.Tiger.Runs.Fast.42",      // Mixed case with numbers
"maple#stream#winter#glow",      // Symbol-separated (new)
```
