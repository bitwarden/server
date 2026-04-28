# Seeder Data System

Structured data generation for realistic vault seeding. Designed for extensibility and spec-driven generation.

## Architecture

Foundation layer for all cipher generation—data and patterns that future cipher types build upon.

- **Enums are the API.** Configure via `CompanyType`, `PasswordStrength`, etc. Everything else is internal.
- **Composable by region.** Arrays aggregate with `[.. UsNames, .. EuropeanNames]`. New region = new array + one line change.
- **Deterministic.** Seeded randomness means same org ID → same test data → reproducible debugging.
- **Filterable.** `Companies.Filter(type, region, category)` for targeted data selection.

## Generators

Seeded, deterministic data generation for cipher content. Orchestrated by `GeneratorContext` which lazy-initializes on first access.

**Adding a generator:** See `GeneratorContext.cs` remarks for the 3-step pattern.

## Distributions

Percentage-based deterministic selection via `Distribution<T>.Select(index, total)`.

## Current Capabilities

### Login Ciphers

- 50 real companies across 3 regions with metadata (category, type, domain)
- Locale-aware name generation via Bogus (Faker) library with 1500-entry pools
- 8 username patterns (corporate email, personal, social, employee ID, etc.)
- 5 password strength levels (138 total passwords)

### Organizational Structures

- Traditional (departments + sub-units)
- Spotify Model (tribes, squads, chapters, guilds)
- Modern/AI-First (feature teams, platform teams, pods)
