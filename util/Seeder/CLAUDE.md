# Bitwarden Seeder Library - Claude Code Configuration

## Quick Reference

**For detailed pattern descriptions (Factories, Recipes, Models, Scenes, Queries, Data), read `README.md`.**

**For detailed usages of the Seeder library, read `util/SeederUtility/README.md` and `util/SeederApi/README.md`**

## Commands

```bash
# Build
dotnet build util/Seeder/Seeder.csproj

# Run tests
dotnet test test/SeederApi.IntegrationTest/

# Run single test
dotnet test test/SeederApi.IntegrationTest/ --filter "FullyQualifiedName~TestMethodName"
```

## Pattern Decision Tree

```
Need to create test data?
├─ ONE entity with encryption? → Factory
├─ ONE cipher from a SeedVaultItem? → CipherSeed.FromSeedItem() + {Type}CipherSeeder.Create()
├─ MANY entities as cohesive operation? → Recipe or Pipeline
├─ Flexible preset-based seeding? → Pipeline (RecipeBuilder + Steps)
├─ Complete test scenario with ID mangling? → Scene
├─ READ existing seeded data? → Query
└─ Data transformation plaintext ↔ encrypted? → Model
```

## Pipeline Architecture

**Modern pattern for composable fixture-based and generated seeding.**

**Flow**: Preset JSON or Options → RecipeOrchestrator → RecipeBuilder → IStep[] → RecipeExecutor → SeederContext → BulkCommitter

**Key actors**:

- **RecipeBuilder**: Fluent API with dependency validation
- **IStep**: Isolated units of work (CreateOrganizationStep, CreateUsersStep, etc.)
- **SeederContext**: Shared mutable state bag (NOT thread-safe)
- **RecipeExecutor**: Executes steps sequentially, captures statistics, commits via BulkCommitter
- **RecipeOrchestrator**: Orchestrates recipe building and execution (from presets or options)
- **SeederDependencies** (`Options/`): Bundles infrastructure services (`DatabaseContext`, `IMapper`, `IPasswordHasher<User>`, `IManglerService`) into a single record. Recipes and the Orchestrator accept this instead of loose parameters. The CLI utility builds it via `SeederServiceFactory.Create().ToDependencies()`.

**Fixture/preset separation**: Fixtures (organizations, rosters, ciphers) are independent and never reference each other. The preset is the only layer that composes fixtures and defines cross-cutting relationships (folder assignments, favorites). See `Seeds/docs/architecture.md`.

**Phase order (org presets)**: Org → OrgApiKey → Roster → Owner (conditional) → Generator (conditional) → Users → Groups → Collections → Folders → Ciphers → CipherCollections → CipherFolders → CipherFavorites → PersonalCiphers
**Phase order (individual presets)**: IndividualUser → NamedFolders → Generator → Folders → Ciphers → FolderAssignments → FavoriteAssignments

**Individual user presets** use the Pipeline with `CreateIndividualUserStep` (no org, no groups, no collections). These presets live in `Seeds/fixtures/presets/individual/` and are identified by having a `"user"` key instead of `"organization"`. They support `folderNames`, `folderAssignments`, and `favoriteAssignments` for fixture-driven personal vault organization. See `Seeds/docs/presets.md` for the catalog.

See `Pipeline/` folder for implementation.

## Parallelism

Steps execute sequentially (phase order preserved by RecipeExecutor). Within a step, `CreateUsersStep` and `GeneratePersonalCiphersStep` use `Parallel.For` internally for CPU-bound Rust FFI work (key generation, encryption).

**Thread-safety requirements:**

- `GeneratorContext` lazy properties (`??=`) must be force-initialized before any `Parallel.For` loop to prevent a data race
- Generators use `ThreadLocal<Faker>` for thread-safe deterministic data generation
- `ManglerService` and `SeederContext` are NOT thread-safe -- pre-compute their outputs before entering parallel loops

## Performance A/B Testing

When measuring step-level performance changes, use paired worktrees:

- Create `server-PM-XXXXX/perf-baseline` and `server-PM-XXXXX/perf-optimized` worktrees
- Both worktrees get `Stopwatch` timing in `RecipeExecutor.Execute()` (the baseline measurement)
- Only the optimized worktree gets actual code changes
- Run presets with `--mangle` flag to avoid DB collisions between runs
- Compare per-step timings across 3+ runs each, discard the first run (JIT warmup)
- `.worktrees/` is already in `.gitignore`

## Density Profiles

Steps accept an optional `DensityProfile` that controls relationship patterns between users, groups, collections, and ciphers. When null, steps use the original round-robin behavior. When present, steps branch into density-aware algorithms.

**Key files**:

- `Options/DensityProfile.cs` — strongly-typed options (public class)
- `Models/SeedPresetDensity.cs` — JSON preset deserialization targets (internal records)
- `Data/Enums/MembershipDistributionShape.cs` — Uniform, PowerLaw, MegaGroup
- `Data/Enums/CollectionFanOutShape.cs` — Uniform, PowerLaw, FrontLoaded
- `Data/Enums/CipherCollectionSkew.cs` — Uniform, HeavyRight
- `Data/Distributions/PermissionDistributions.cs` — 11 named distributions by org tier

**Backward compatibility contract**: `DensityProfile? == null` MUST produce identical output to the original code. Every step guards this with `if (_density == null) { /* original path */ }`.

**Preset JSON**: Add an optional `"density": { ... }` block. See `Seeds/schemas/preset.schema.json` for the full schema.

**Presets**: Organized into `features/`, `qa/`, `scale/`, `validation/` folders under `Seeds/fixtures/presets/`. See `Seeds/docs/presets.md` for the full catalog.

**Verification**: SQL queries for validating density algorithms are in `Seeds/docs/verification.md`.

## The Recipe Contract

Recipes follow strict rules:

1. A Recipe SHALL accept `SeederDependencies` as its single constructor parameter
2. A Recipe SHALL have exactly one public method named `Seed()`
3. A Recipe MUST produce one cohesive result
4. A Recipe MAY have overloaded `Seed()` methods with different parameters
5. A Recipe SHALL use private helper methods for internal steps
6. A Recipe SHALL use BulkCopy for performance when creating multiple entities
7. A Recipe SHALL compose Factories for individual entity creation
8. A Recipe SHALL NOT expose implementation details as public methods

## Zero-Knowledge Architecture

**Critical:** Unencrypted vault data never leaves the client. The server never sees plaintext.

The Seeder uses the Rust SDK via FFI because it must behave like a real Bitwarden client:

1. Generate encryption keys (like client account setup)
2. Encrypt vault data client-side (same SDK as real clients)
3. Store only encrypted result

## Data Flow

### Pipeline path (fixture → entity)

```
SeedVaultItem → CipherSeed.FromSeedItem() → CipherSeed → {Type}CipherSeeder.Create(options) → CipherViewDto → encrypt_fields (Rust FFI) → EncryptedCipherDto → EncryptedCipherDtoExtensions → Server Cipher Entity
```

### Core encryption (shared by all paths)

```
CipherViewDto → JSON + [EncryptProperty] field paths → encrypt_fields (Rust FFI, bitwarden_crypto) → EncryptedCipherDto → EncryptedCipherDtoExtensions → Server Cipher Entity
```

Shared logic: `Factories/CipherEncryption.cs`, `Models/EncryptedCipherDtoExtensions.cs`

## Rust Crypto Dependency

The Rust shim (`util/RustSdk/rust/`) depends only on `bitwarden_crypto`. It does **not** depend on `bitwarden_vault` — the seeder drives field selection via `[EncryptProperty]` attributes, not SDK cipher types.

Before modifying encryption integration, run `RustSdkCipherTests` to validate roundtrip encryption.

## Deterministic Data Generation

Same domain = same seed = reproducible data:

```csharp
var seed = options.Seed ?? DeriveStableSeed(options.Domain);
```

## Scenarios

Developer-facing documentation in `Seeds/docs/scenarios/`. Each file maps an engineering problem to a Seeder command.

**Maintenance rules:**

- When adding a new preset, check if an existing scenario should reference it as a variation
- When adding a new command or flag, check if it enables a new scenario or changes an existing one
- When CLI flags, commands, or preset names change, scan all `*.md` files under `Seeds/` and `SeederUtility/` for stale references
- Scenario files follow the template in `Seeds/docs/scenarios/README.md`
- Never duplicate CLI flag documentation — link to `SeederUtility/README.md`
- Never duplicate preset catalog details — link to `Seeds/docs/presets.md`
- Scenarios describe _why_ (the problem). READMEs describe _how_ (the tool). Keep the split clean.

**File relationships:**

- `SeederUtility/README.md` → CLI reference (commands, flags, examples) → links to scenarios
- `Seeds/docs/presets.md` → what exists (the catalog) → scenarios link back to it
- `Seeds/docs/scenarios/` → why you'd use it (problem → command)

## Collection Management Settings

**Collection management settings are not plan-gated.** `AllowAdminAccessToAllCollectionItems`, `LimitCollectionCreation`, `LimitCollectionDeletion`, and `LimitItemDeletion` apply identically across all plan types. They are org-level admin settings, not billing-plan features.

**These settings alter access control behavior.** When seeding scenarios that test member vs. admin permissions, collection creation/deletion policies, or item-level access, set them explicitly in the preset rather than relying on defaults.

**Configurable in presets and CLI.** Use the JSON preset `organization` block (e.g. `"limitCollectionCreation": true`) or the CLI flags: `--limit-collection-creation`, `--limit-collection-deletion`, `--limit-item-deletion`, `--allow-admin-collection-access`.

## Security Reminders

- Default test password: `asdfasdfasdf` (overridable via `--password` CLI flag or `SeederSettings`)
- Never commit database dumps with seeded data
- Seeded keys are for testing only
