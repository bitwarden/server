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

**Flow**: Preset JSON → PresetLoader → RecipeBuilder → IStep[] → RecipeExecutor → SeederContext → BulkCommitter

**Key actors**:

- **RecipeBuilder**: Fluent API with dependency validation
- **IStep**: Isolated unit of work (CreateOrganizationStep, CreateUsersStep, etc.)
- **RecipeExecutor**: Executes steps, captures statistics, commits
- **RecipeOrchestrator**: Orchestrates recipe building and execution (from presets or options)
- **SeederContext**: Shared mutable state (NOT thread-safe)

**Why this architecture wins**:

- **Infrastructure as Code**: JSON presets define complete scenarios
- **Mix & Match**: Fixtures + generation in one preset
- **Extensible**: Add entity types via new IStep implementations
- **Future-ready**: Supports custom DSLs on top of RecipeBuilder

**Phase order**: Org → Owner → Generator → Roster → Users → Groups → Collections → Folders → Ciphers → PersonalCiphers

**Naming**: `{Purpose}Step` classes implementing `IStep`

**Files**: `Pipeline/` folder

---

#### Factories

**Purpose:** Create individual domain entities with cryptographically correct encrypted data.

**Metaphor:** Skilled craftspeople who create one perfect item per call.

**When to use:** Need to create ONE entity (user, cipher, collection) with proper encryption.

**Key characteristics:**

- Create ONE entity per method call
- Handle encryption/transformation internally
- Stateless (except for SDK service dependency)
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
- Receive `IManglerService` via DI for test isolation—service handles mangling automatically
- Return `SceneResult` with MangleMap (original→mangled) for test assertions
- Async operations
- CAN modify database state

**Naming:** `{Scenario}Scene` class with `SeedAsync(Request)` method (defined by interface)

#### Queries

**Purpose:** Read-only data retrieval for test assertions and verification.

**Metaphor:** Information desks that answer questions without changing anything.

**When to use:** Need to READ existing seeded data for verification or follow-up operations.

**Example:** Inviting a user to an organization produces a magic link to accept the invite, a query should be used to retrieve that link because it is easier than interfacing with an external smtp catcher.

**Key characteristics:**

- Implement `IQuery<TRequest, TResult>`
- Read-only (no database modifications)
- Return typed data for test assertions
- Can be used to retrieve side effects due to tested flows

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

**Folder structure:** See `Data/README.md` for Generators and Distributions details.

- `Static/` - Read-only data arrays (Companies, Passwords, Names, OrgStructures)
- `Generators/` - Seeded data generators via `GeneratorContext`
- `Distributions/` - Percentage-based selection via `Distribution<T>`
- `Enums/` - Public API enums

#### Services

**Purpose:** Injectable services that provide cross-cutting functionality via dependency injection.

**`IManglerService`** - Context-aware string mangling for test isolation:

- `Mangle(string)` - Transforms strings with unique prefixes for collision-free test data
- `GetMangleMap()` - Returns dictionary of original → mangled mappings for assertions
- `IsEnabled` - Indicates whether mangling is active

**Implementations:**

- `ManglerService` - Scoped stateful service that adds unique prefixes (`{prefix}+user@domain` for emails, `{prefix}-value` for strings)
- `NoOpManglerService` - Singleton no-op service that returns values unchanged

**Configuration:**

- SeederApi: Enabled when `GlobalSettings.TestPlayIdTrackingEnabled` is true
- SeederUtility: Enabled with `--mangle` CLI flag

---

## Rust SDK Integration

The seeder uses FFI calls to the Rust SDK for cryptographically correct encryption:

```
CipherViewDto → RustSdkService.EncryptCipher() → EncryptedCipherDto → Server Format
```

This ensures seeded data can be decrypted and displayed in the actual Bitwarden clients.
