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
├─ MANY entities as cohesive operation? → Recipe or Pipeline
├─ Flexible preset-based seeding? → Pipeline (RecipeBuilder + Steps)
├─ Complete test scenario with ID mangling? → Scene
├─ READ existing seeded data? → Query
└─ Data transformation SDK ↔ Server? → Model
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

**Phase order**: Org → Owner → Generator → Roster → Users → Groups → Collections → Folders → Ciphers → PersonalCiphers

See `Pipeline/` folder for implementation.

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

**Validation presets**: `Seeds/fixtures/presets/validation/` contains presets that verify density algorithms produce correct distributions. See the README in that folder for queries and expected results.

## The Recipe Contract

Recipes follow strict rules:

1. A Recipe SHALL have exactly one public method named `Seed()`
2. A Recipe MUST produce one cohesive result
3. A Recipe MAY have overloaded `Seed()` methods with different parameters
4. A Recipe SHALL use private helper methods for internal steps
5. A Recipe SHALL use BulkCopy for performance when creating multiple entities
6. A Recipe SHALL compose Factories for individual entity creation
7. A Recipe SHALL NOT expose implementation details as public methods

## Zero-Knowledge Architecture

**Critical:** Unencrypted vault data never leaves the client. The server never sees plaintext.

The Seeder uses the Rust SDK via FFI because it must behave like a real Bitwarden client:

1. Generate encryption keys (like client account setup)
2. Encrypt vault data client-side (same SDK as real clients)
3. Store only encrypted result

## Data Flow

```
CipherViewDto → Rust SDK encrypt_cipher → EncryptedCipherDto → TransformToServer → Server Cipher Entity
```

Shared logic: `CipherEncryption.cs`, `EncryptedCipherDtoExtensions.cs`

## Rust SDK Version Alignment

| Component   | Version Source                            |
| ----------- | ----------------------------------------- |
| Server Shim | `util/RustSdk/rust/Cargo.toml` git rev    |
| Clients     | `@bitwarden/sdk-internal` in clients repo |

Before modifying SDK integration, run `RustSdkCipherTests` to validate roundtrip encryption.

## Deterministic Data Generation

Same domain = same seed = reproducible data:

```csharp
_seed = options.Seed ?? StableHash.ToInt32(options.Domain);
```

## Security Reminders

- Default test password: `asdfasdfasdf` (overridable via `--password` CLI flag or `SeederSettings`)
- Never commit database dumps with seeded data
- Seeded keys are for testing only
