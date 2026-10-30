# Seeder Data System

Structured data generation for realistic vault seeding. Designed for extensibility and spec-driven generation.

## Architecture

Foundation layer for all cipher generation—data and patterns that future cipher types build upon.

- **Enums are the API.** Configure via `CompanyType`, `PasswordStrength`, etc. Everything else is internal.
- **Region sets locale and formats.** `GeographicRegion` sets the name locale for login usernames, the card brands, and identity names, titles, addresses, country code and SSN format. It does not filter companies: logins always draw from `Companies.All`.
- **Deterministic.** Seeded randomness means same org ID → same test data → reproducible debugging.

## Generators

Seeded, deterministic data generation for cipher content. Orchestrated by `GeneratorContext` which lazy-initializes on first access.

**Adding a generator:** See `GeneratorContext.cs` remarks for the 3-step pattern.

## Distributions

Percentage-based deterministic selection — `Distribution<T>.Select(index, total)` for typed buckets (throws unless
`0 <= index < total`), or
index-set selection like `ArchiveDeleteDistribution.Select(...)` for lifecycle-state assignment.

## Current Capabilities

### Login Ciphers

- 314 companies across 6 regions (`.example` domains) with metadata (category, type, domain)
- Locale-aware name generation via Bogus (Faker) library with 1500-entry pools
- 8 username patterns (corporate email, personal, social, employee ID, etc.)
- 5 password strength levels: shared pools (138 passwords) plus per-item Fair, Strong and VeryStrong passwords from `UniquePasswordGenerator`

#### Calibrated login rates

`CipherComposer` constants, measured on a large EU org (2026-10-11). They apply to every preset that generates ciphers.
Re-measure before changing them.

| Constant                                               | Value  | Source                                                                                                                                                                                                                                                                                        |
| ------------------------------------------------------ | ------ | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `UriPer100Logins` / `MultiUriPer100Logins`             | 32 / 1 | Logins with a URI / two URIs in the target org (fleet: 86% / 2.4%)                                                                                                                                                                                                                            |
| `NotesPer100Logins`                                    | 37     | Target org (fleet: 8%). Note lengths have a long tail: 70% short, 25% a paragraph, 5% runbook-sized                                                                                                                                                                                           |
| `FieldsPer100Logins` / `TotpPer100Logins`              | 6 / 2  | Target org (fleet: 4% / 2.1%)                                                                                                                                                                                                                                                                 |
| `PasskeysPer100Logins` / `PasswordHistoryPer100Logins` | 1 / 6  | Prod: passkeys on 0.2-1.2% of logins, history on 5-7%                                                                                                                                                                                                                                         |
| `ItemKeysPer100`                                       | 55     | Generated items of every type with their own key (`Cipher.Key`), as in the target org (fleet: 33%)                                                                                                                                                                                            |
| `AppsPer100Logins`                                     | 24     | Large-org median of applications per password in the Access Intelligence report. Rounded up to whole passes over the company list. The first pass keeps each company's domain; later passes use `{name}-{n}.example`. The personal step sizes passes on every user's personal logins together |
| `UniquePasswordsPer100FairOrStronger`                  | 80     | **Assumption**: prod reuse can't be counted because passwords are encrypted per item. Applies only to logins drawn Fair or stronger; VeryWeak and Weak draws stay pooled because zxcvbn scores no unique password as low as the pooled ones. Drives breach-check (HIBP) volume                |

Stored login `Data` omits the legacy top-level `Uri`, as the API does.

#### Unique password shapes

`UniquePasswordGenerator` builds a password from the seed and the login's index, so a login's password is practically
unique but reproducible. The shapes follow the pools:

| Tier       | Shape                                                                 |
| ---------- | --------------------------------------------------------------------- |
| Fair       | one of the first 48 `PasswordWords`, capitalized, plus up to 5 digits |
| Strong     | half 16 random characters, half 3 words from the 256 `PasswordWords`  |
| VeryStrong | half 22 random characters, half 4 or 5 words                          |

Words are joined with one of `-`, `.`, `#`, `@` or `$`, the separators the pools use. No pooled passphrase can be
spelled from `PasswordWords`, so a passphrase never equals a pooled one.

Fair uses only the first 48 words because zxcvbn scores the rarer words higher: with all 256, about 60% of Fair
passwords scored 3 against 27% in the pool (2,000 samples).

Measured 2026-10-23 with npm `zxcvbn@4.4.2`, the version in the clients root `package.json`: dump 20,000 passwords per
tier for two seeds (42 and 7) with `GenerateByIndex(0..19999, tier)`, score each, and compare the percentage at each
score with the tier's pool. The shapes stay within 15 points of the pools.

| Tier       | Pool, % at score 1 / 2 / 3 / 4 | Unique, seeds 42 and 7                          |
| ---------- | ------------------------------ | ----------------------------------------------- |
| Fair       | 6.7 / 66.7 / 26.7 / 0          | 4.4 / 72.0 / 23.6 / 0 and 4.6 / 71.9 / 23.6 / 0 |
| Strong     | 0 / 0 / 0 / 100                | 0.1 / 0.3 or 0.4 / 99.6 at scores 2 / 3 / 4     |
| VeryStrong | 0 / 0 / 0 / 100                | 100 at score 4, both seeds                      |

Re-measure after changing a shape or the word list.

### Organizational Structures

- Traditional (departments + sub-units)
- Spotify Model (tribes, squads, chapters, guilds)
- Modern/AI-First (feature teams, platform teams, pods)
