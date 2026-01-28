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

### Key Hierarchy

```
Organization Key (or User Key)
       │
       ├──▶ Encrypts Cipher.Key (optional per-cipher key)
       │
       └──▶ Encrypts cipher fields directly (if no per-cipher key)
```

For seeding, we encrypt directly with the organization key.

### Entity Relationships

```
Organization
    │
    ├── Collections ──┬── CollectionCipher ──┐
    │                 │                      │
    └── Ciphers ──────┴──────────────────────┘
```

Ciphers belong to organizations and are assigned to collections via the `CollectionCipher` join table.

### Project Structure

The Seeder is organized around six core patterns, each with a specific responsibility:

#### Factories

**Purpose:** Create individual domain entities with cryptographically correct encrypted data.

**Metaphor:** Skilled craftspeople who create one perfect item per call.

**When to use:** Need to create ONE entity (user, cipher, collection) with proper encryption.

**Key characteristics:**

- Create ONE entity per method call
- Handle encryption/transformation internally
- Stateless (except for SDK service dependency)
- Return fully-formed entity ready for persistence
- Do NOT interact with database directly

**Naming:** `{Entity}Seeder` class with `Create{Type}{Entity}()` methods

---

#### Recipes

**Purpose:** Orchestrate cohesive bulk operations using BulkCopy for performance.

**Metaphor:** Cooking recipes that produce one complete result through coordinated steps. Like baking a three-layer cake - you don't grab three separate recipes and stack them; you follow one comprehensive recipe that orchestrates all the steps.

**When to use:** Need to create MANY related entities as one cohesive operation (e.g., organization + users + collections + ciphers).

**Key characteristics:**

- Orchestrate multiple entity creations as a cohesive operation
- Use BulkCopy for performance optimization
- Interact with database directly
- Compose Factories for individual entity creation
- **SHALL have a `Seed()` method** that executes the complete recipe
- Use method parameters (with defaults) for variations, not separate methods

**Naming:** `{DomainConcept}Recipe` class with primary `Seed()` method

**Note:** Some existing recipes violate the `Seed()` method convention and will be refactored in the future.

---

#### Models

**Purpose:** DTOs that bridge the gap between SDK encryption format and server storage format.

**Metaphor:** Translators between two different languages (SDK format vs. Server format).

**When to use:** Need data transformation during the encryption pipeline (SDK → Server format).

**Key characteristics:**

- Pure data structures (DTOs)
- No business logic
- Handle serialization/deserialization
- Bridge SDK ↔ Server format differences

#### Scenes

**Purpose:** Create complete, isolated test scenarios for integration tests.

**Metaphor:** Theater scenes with multiple actors and props arranged to tell a complete story.

**When to use:** Need a complete test scenario with proper ID mangling for test isolation.

**Key characteristics:**

- Implement `IScene<TRequest>` or `IScene<TRequest, TResult>`
- Create complete, realistic test scenarios
- Handle ID mangling for test isolation
- Return `SceneResult` with mangle map for test assertions
- Async operations
- CAN modify database state

**Naming:** `{Scenario}Scene` class with `SeedAsync(Request)` method (defined by interface)

#### Queries

**Purpose:** Read-only data retrieval for test assertions and verification.

**Metaphor:** Information desks that answer questions without changing anything.

**When to use:** Need to READ existing seeded data for verification or follow-up operations.

**Key characteristics:**

- Implement `IQuery<TRequest, TResult>`
- Synchronous operations (vs. async Scenes)
- Read-only (no database modifications)
- Return typed data for test assertions
- Often used to verify Scene/Recipe results

**Naming:** `{DataToRetrieve}Query` class with `Execute(Request)` method (defined by interface)

#### Data

**Purpose:** Reusable, realistic test data collections that provide the foundation for cipher generation.

**Metaphor:** A well-stocked ingredient pantry that all recipes draw from.

**When to use:** Need realistic, filterable data for cipher content (company names, passwords, usernames).

**Key characteristics:**

- Static readonly arrays and classes
- Filterable by region, type, category
- Deterministic (seeded randomness for reproducibility)
- Composable across regions
- Enums provide the public API (CompanyType, PasswordStrength, etc.)

## Rust SDK Integration

The seeder uses FFI calls to the Rust SDK for cryptographically correct encryption:

```
CipherViewDto → RustSdkService.EncryptCipher() → EncryptedCipherDto → Server Format
```

This ensures seeded data can be decrypted and displayed in the actual Bitwarden clients.
