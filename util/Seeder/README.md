# Bitwarden Database Seeder

A class library for generating and inserting properly encrypted test data into Bitwarden databases.

## Domain Taxonomy

### Cipher Encryption States

| Term           | Description                                          | Stored in DB? |
| -------------- | ---------------------------------------------------- | ------------- |
| **CipherView** | Plaintext/decrypted form. Human-readable data.       | Never         |
| **Cipher**     | Encrypted form. All sensitive fields are EncStrings. | Yes           |

The "View" suffix always denotes plaintext. No suffix means encrypted.

### Data Structure Differences

**SDK Structure (nested):**

```json
{ "name": "2.x...", "login": { "username": "2.y...", "password": "2.z..." } }
```

**Server Structure (flat, stored in Cipher.Data):**

```json
{ "Name": "2.x...", "Username": "2.y...", "Password": "2.z..." }
```

The seeder transforms SDK output to server format before database insertion.

### Project Structure

The Seeder is organized around six core patterns, each with a specific responsibility:

#### Pipeline

**Purpose:** Composable architecture for fixture-based and generated seeding.

**When to use:** New bulk operations, especially with presets. Provides ultimate flexibility.

**Flow**: Preset JSON → Loader → Builder → Steps → Executor → Context → BulkCommitter

**Why this architecture wins**:

- **Infrastructure as Code**: JSON presets define complete scenarios
- **Mix & Match**: Fixtures + generation in one preset
- **Extensible**: Add entity types via new step implementations

**Phase order (org)**: Org → Roster → Owner (conditional) → Generator (conditional) → Users → Groups → Collections → Folders → Ciphers → CipherCollections → CipherFolders → CipherFavorites → PersonalCiphers
**Phase order (individual)**: IndividualUser → NamedFolders → Generator → Folders → Ciphers → FolderAssignments → FavoriteAssignments

**Files**: `Pipeline/` folder

#### Factories

**Purpose:** Create individual domain entities with cryptographically correct encrypted data.

**When to use:** Need to create ONE entity (user, cipher, collection) with proper encryption.

**Key characteristics:**

- Create ONE entity per method call
- Handle encryption/transformation internally
- Stateless (except for SDK service dependency)
- Do NOT interact with database directly

**Naming:** `{Entity}Seeder` with `Create{Type}{Entity}()` methods

#### Recipes

**Purpose:** Orchestrate cohesive bulk operations using BulkCopy for performance.

**When to use:** Need to create MANY related entities as one cohesive operation.

**Key characteristics:**

- Orchestrate multiple entity creations as a cohesive operation
- Use BulkCopy for performance optimization
- Interact with database directly
- Compose Factories for individual entity creation
- **SHALL have a `Seed()` method** that executes the complete recipe
- Use method parameters (with defaults) for variations, not separate methods

**Naming:** `{DomainConcept}Recipe` with a `Seed()` method

#### Models

**Purpose:** DTOs that transform plaintext cipher data into encrypted form for database storage.

**When to use:** Need to convert `CipherViewDto` to `EncryptedCipherDto` during the encryption pipeline.

**Key characteristics:**

- Pure data structures (DTOs)
- No business logic
- Handle serialization/deserialization (camelCase ↔ PascalCase)
- Mark encryptable fields with `[EncryptProperty]` attribute

#### Scenes

**Purpose:** Create complete, isolated test scenarios for integration tests.

**When to use:** Need a complete test scenario with proper ID mangling for test isolation.

**Key characteristics:**

- Complete, realistic test scenarios with ID mangling for isolation
- Receive mangling service via DI — returns a map of original→mangled values for assertions
- CAN modify database state

**Naming:** `{Scenario}Scene` with a `SeedAsync()` method

#### Queries

**Purpose:** Read-only data retrieval for test assertions and verification.

**When to use:** Need to READ existing seeded data for verification or follow-up operations.

**Key characteristics:**

- Read-only (no database modifications)
- Return typed data for test assertions

**Naming:** `{DataToRetrieve}Query` with an `Execute()` method

#### Data

**Purpose:** Reusable, realistic test data collections that provide the foundation for cipher generation.

**When to use:** Need realistic, filterable data for cipher content (company names, passwords, usernames).

**Key characteristics:**

- Static readonly arrays and classes
- Filterable by region, type, category
- Deterministic (seeded randomness for reproducibility)
- Composable across regions
- Enums provide the public API

See `Data/README.md` for Generators and Distributions details.

#### Services

**Purpose:** Injectable services that provide cross-cutting functionality via dependency injection.

Context-aware string mangling for test isolation. Adds unique prefixes to emails and strings for collision-free test data. Enabled via `--mangle` CLI flag (SeederUtility) or application settings (SeederApi).

## Rust SDK Integration

The seeder uses FFI calls to the Rust SDK for cryptographically correct encryption:

```
CipherViewDto → encrypt_fields (field-level encryption via bitwarden_crypto) → EncryptedCipherDto → Server Format
```

This ensures seeded data can be decrypted and displayed in the actual Bitwarden clients.
