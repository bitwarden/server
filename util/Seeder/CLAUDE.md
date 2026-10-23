# Bitwarden Seeder Library - Claude Code Configuration

## Quick Reference

**For detailed pattern descriptions (Factories, Recipes, Models, Scenes, Queries, Data), read `README.md`.**

**For detailed usages of the Seeder library, read `util/SeederUtility/README.md` and `util/SeederApi/README.md`**

**To seed a database, use the `seeding-bitwarden-data` skill.** It picks a preset first, runs the CLI, and lists the logins.

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

```text
Need to create test data?
├─ ONE entity with encryption? → Factory
├─ ONE cipher from a SeedVaultItem? → CipherSeed.FromSeedItem() + {Type}CipherSeeder.Create()
├─ MANY entities as cohesive operation? → Recipe or Pipeline
├─ Flexible preset-based seeding? → Pipeline (RecipeBuilder + Steps)
├─ Complete test scenario with ID mangling? → Scene
├─ READ existing seeded data? → Query
└─ Plaintext or encrypted cipher DTO? → Model (Factories/CipherEncryption.cs encrypts)
```

## Pipeline Architecture

The Pipeline composes steps for both fixture-based and generated seeding.

**Flow**: Preset JSON or Options → RecipeOrchestrator → PresetLoader (presets only) → RecipeBuilder → IStep/IAsyncStep[] → RecipeExecutor → SeederContext → BulkCommitter → IPostCommitStep[]

**Key actors**:

- **RecipeBuilder**: Fluent API with dependency validation
- **IStep / IAsyncStep**: Isolated units of work (CreateOrganizationStep, CreateUsersStep, etc.). Use `IAsyncStep` for steps that do real I/O. A step additionally marked `IPostCommitStep` is deferred until after the bulk commit, so it observes committed rows. Its entity lists are already cleared, since only the `EntityRegistry` and the context's scalar properties survive the commit.
- **SeederContext**: Shared mutable state bag (NOT thread-safe)
- **RecipeExecutor**: Awaits steps sequentially, captures statistics, commits via BulkCommitter, then runs any post-commit steps
- **RecipeOrchestrator**: Orchestrates recipe building and execution (from presets or options)
- **SeederDependencies** (`Options/`): one record of the infrastructure services that `OrganizationRecipe`, `IndividualUserRecipe`, and the Orchestrator take instead of loose parameters. `ILicensingService` and the two Stripe billing services are factories: `LicensingService` throws on construction when the licensing certificate cannot be loaded, and only commands that opt into billing should build the billing DI graph. The CLI builds the record via `SeederServiceFactory.Create(...).ToDependencies()`. Add new optional services as `init` properties rather than growing the positional list.

**Keep both step interfaces; don't unify them into one async contract.** Collapsing to one `Task ExecuteAsync(SeederContext)` costs: rewrite every step class in `Steps/` plus the step test doubles in `test/SeederApi.IntegrationTest/`; force every `.Execute(context)` site in `test/SeederApi.IntegrationTest/Steps/` to `await`, their test methods to `async`; and `TreatWarningsAsErrors` is on repo-wide (`Directory.Build.props`), so CS1998 makes `async` without `await` a build error, which means every sync step needs `return Task.CompletedTask`. The split costs less: two-arm union in `OrderedStep`, `object`-typed `Inner`, one duplicated `RecipeBuilder` registration. `IScene` and `IQuery` differ: each has one `Task`-returning method and no sync twin.

**Fixture/preset separation**: Fixtures (organizations, rosters, ciphers) are independent and never reference each other. The preset is the only layer that composes fixtures and defines cross-cutting relationships (folder assignments, favorites). See `Seeds/docs/architecture.md`.

**Step order**: steps run in the order the builder registers them. `Pipeline/PresetLoader.cs` sets it for presets, and `Pipeline/RecipeOrchestrator.cs` sets it for the `organization` and `individual` options.

**Individual user presets** use the Pipeline with `CreateIndividualUserStep` (no org, no groups, no collections). These presets live in `Seeds/fixtures/presets/individual/` and are identified by having a `"user"` key instead of `"organization"`. They support `folderNames`, `folderAssignments`, and `favoriteAssignments` for fixture-driven personal vault organization. See `Seeds/docs/presets.md` for the catalog.

See `Pipeline/` folder for implementation.

## Parallelism

Steps execute sequentially (phase order preserved by RecipeExecutor). Async steps are awaited one at a time and MUST NOT be batched with `Task.WhenAll`, because each step reads state written by the ones before it. Within a step, `CreateUsersStep` and `GeneratePersonalCiphersStep` use `Parallel.For` internally for CPU-bound Rust FFI work (key generation, encryption).

**Thread-safety requirements:**

- `GeneratorContext` lazy properties (`??=`) must be force-initialized before any `Parallel.For` loop to prevent a data race
- Faker-backed cipher data generators use `ThreadLocal<Faker>` for thread-safe deterministic data generation
- `ManglerService` and `SeederContext` are NOT thread-safe. Pre-compute their outputs before entering parallel loops

## Performance A/B Testing

When measuring step-level performance changes, use paired worktrees:

- Create `.worktrees/PM-XXXXX-perf-baseline` and `.worktrees/PM-XXXXX-perf-optimized` worktrees
- Both worktrees get `Stopwatch` timing in `RecipeExecutor.ExecuteAsync()` (the baseline measurement)
- Only the optimized worktree gets actual code changes
- Run presets with `--mangle` flag to avoid DB collisions between runs
- Compare per-step timings across 3+ runs each, discard the first run (JIT warmup)
- `.worktrees/` is already in `.gitignore`

## Density Profiles

Density-aware steps accept an optional `DensityProfile` that controls relationship patterns between users, groups, collections, and ciphers. When null, they keep their original behavior. When present, they branch into density-aware algorithms.

**Key files**:

- `Options/DensityProfile.cs` — strongly-typed options (public class)
- `Models/SeedPresetDensity.cs` — JSON preset deserialization targets (internal records)
- `Data/Enums/MembershipDistributionShape.cs` — Uniform, PowerLaw, MegaGroup
- `Data/Enums/CollectionFanOutShape.cs` — Uniform, PowerLaw, FrontLoaded
- `Data/Enums/CipherCollectionSkew.cs` — Uniform, HeavyRight
- `Data/Distributions/PermissionDistributions.cs` — 11 named distributions by org tier

**Backward compatibility contract**: `DensityProfile? == null` MUST produce identical output to the original code. Each density-aware step keeps the null path separate, with an explicit `null` branch or `density?.X ?? default` fallbacks.

**Preset JSON**: Add an optional `"density": { ... }` block. See `Seeds/schemas/preset.schema.json` for the full schema.

**Presets**: Organized into `dev/`, `features/`, `qa/`, `scale/`, `individual/`, `validation/` folders under `Seeds/fixtures/presets/`. See `Seeds/docs/presets.md` for the full catalog.

**Verification**: SQL queries for validating density algorithms are in `Seeds/docs/verification.md`.

## Regression Testing

Changes to `Factories/`, `Steps/`, `Scenes/`, or `Recipes/` need a regression pass against a live local SQL Server database. `Seeds/docs/regression.md` maps each changed path to the preset that reaches it and the assertion that proves it, and lists the known non-regressions. Claude drives the CLI, API, and SQL (CLI via `seeding-bitwarden-data`, SQL via `exploring-bitwarden-data`); the developer smoke-tests the web vault.

## Data/ File Organization

New files under `Data/` belong in the matching subfolder (`Distributions/`, `Enums/`, `Generators/`, `Static/`), never loose at the top level. If a new file fits no existing subfolder, create one. `Data/README.md` describes the generators and distributions.

**Two Enums homes, by concern:** `Data/Enums/` (namespace `Bit.Seeder.Data.Enums`) holds the generation-config surface (`CompanyType`, `PasswordStrength`, distribution shapes, etc. — "Enums are the API"). Crypto-taxonomy enums that describe how seeded vault data is encrypted (`CipherEncryptionType`, `AttachmentSchemeType`) live in the top-level `Enums/` folder (namespace `Bit.Seeder.Enums`), one enum per file.

## The Recipe Contract

Recipes follow strict rules:

1. A Pipeline-backed Recipe SHALL accept `SeederDependencies` as its single constructor parameter. The direct-to-database Recipes take `DatabaseContext` and the services they need.
2. A Recipe SHALL have one public entry-point name: `Seed()` when synchronous, `SeedAsync()` when it returns `Task`/`Task<T>`. Pipeline-backed Recipes (`OrganizationRecipe`, `IndividualUserRecipe`) are async; the direct-to-database Recipes (`CollectionsRecipe`, `GroupsRecipe`, `OrganizationDomainRecipe`, `OrganizationWithUsersRecipe`) remain synchronous.
3. A Recipe MUST produce one cohesive result
4. A Recipe MAY overload that entry point with different parameters
5. A Recipe SHALL use private helper methods for internal steps
6. A Recipe SHALL use BulkCopy for performance when creating multiple entities
7. A Pipeline-backed Recipe SHALL create entities through `RecipeOrchestrator` steps, which compose Factories
8. A Recipe SHALL NOT expose implementation details as public methods

## Zero-Knowledge Architecture

**Critical:** Unencrypted vault data never leaves the client. The server never sees plaintext.

The Seeder uses the Rust SDK via FFI because it must behave like a real Bitwarden client:

1. Generate encryption keys (like client account setup)
2. Encrypt vault data client-side (same SDK as real clients)
3. Store only encrypted result

## Server Data Protection

`User.Key` and `User.MasterPassword` pass through the EF `DataProtectionConverter`, so a default seed stores `P|` values that only a server holding the seeding machine's data-protection keys can read. `--no-data-protection` (`SeederDependencies.DisableDataProtection`) makes `BulkCommitter` insert users as `Pipeline/UserBulkRow.cs` rows instead, which skips the converter; the server reads unprefixed values as-is and re-protects them on the next save. SQL Server only: `RecipeOrchestrator` throws on other providers. Use it only against DEV/QA databases that hold seeded accounts.

## Data Flow

### Pipeline path (fixture → entity)

```text
SeedVaultItem → CipherSeed.FromSeedItem() → CipherSeed → {Type}CipherSeeder.Create(options) → CipherViewDto → core encryption (below)
```

### Core encryption (shared by all paths)

```text
CipherViewDto → JSON + [EncryptProperty] field paths → encrypt_fields, or encrypt_fields_with_cipher_key for cipherKey ciphers (Rust FFI, bitwarden_crypto) → EncryptedCipherDto → EncryptedCipherDtoExtensions → Server Cipher Entity
```

Shared logic: `Factories/CipherEncryption.cs`, `Models/EncryptedCipherDtoExtensions.cs`

Every login URI must carry `uriChecksum` (set in `LoginCipherSeeder`): the SDK silently drops URIs without a valid checksum from items that have their own key.

## Rust Crypto Dependency

The Rust shim (`util/RustSdk/rust/`) uses `bitwarden_crypto` and no other Bitwarden SDK crate. It does **not** depend on `bitwarden_vault`, because the seeder drives field selection via `[EncryptProperty]` attributes, not SDK cipher types.

Before modifying encryption integration, run `RustSdkCipherTests` to validate roundtrip encryption.

## Encryption Schemes (crypto taxonomy)

Seeded data spans two orthogonal encryption axes, defined in `Enums/CipherEncryptionType.cs` and `Enums/AttachmentSchemeType.cs`:

- **Cipher encryption** (`cipherEncryption`): `userKey` (no cipher key; `Cipher.Key` null) or `cipherKey` (per-cipher key wrapped by the vault key).
- **Attachment scheme version** (`attachmentVersion`): `v0` (no attachment key), `v1` (attachment key wrapped by the vault key), `v2` (attachment key wrapped by the cipher key).

**Invariant:** a cipher and its attachments use the same strategy: `v2` requires a `cipherKey` host. `Steps/CreateCipherAttachmentsStep.cs` and `Seeds/schemas/cipher.schema.json` both enforce this; keep them in sync.

**Wire mapping:** `AttachmentSchemeType.{V0,V1,V2}` casts to `u32 {0,1,2}` and is matched verbatim in `util/RustSdk/rust/src/attachment.rs`. The value *is* the version number. Do not reintroduce an offset.

**Do not conflate with account Encryption V1/V2.** Attachment `v0/v1/v2` is key-wrapping only. Every symmetric `EncString` the seeder emits is Encryption-V1 type 2 (AES-256-CBC-HMAC). Org keys shared to members are RSA type 4, and no COSE/type-7 path exists. A future V2/COSE capability is a **separate** axis (a new enum), never a new attachment version.

## Deterministic Data Generation

Same domain = same seed = reproducible data:

```csharp
var seed = options.Seed ?? DeriveStableSeed(options.Domain);
```

Per-item generators seed with `ItemSeedMixer.Mix(seed, index)`, never `seed + index`: the sum gives seed `s` at index `i + 1` the same stream as seed `s + 1` at index `i`.

**Distribution totals are explicit.** `Distribution<T>.Select(index, total)` throws unless `0 <= index < total`. `CipherComposer.Compose(index, total, ...)` takes the modulus for a login's username, password strength and pooled password (`index % total`, so they repeat every `total` items); unique passwords, URIs, notes, fields, TOTP, item keys and the other cipher types use `index`. The steps pass the number of logins (`CipherComposer.CountLogins`): logins are one contiguous run of the type distribution, so a larger total would give them only its first buckets (no Strong or VeryStrong passwords). Don't derive the total from the generator: preset recipes build it without a cipher count, and a zero total once put every generated preset login in the last password bucket (24 distinct "very strong" passwords).

**Generated content rates are calibrated to prod.** The `*Per100` constants in `CipherComposer` apply to every preset
that generates ciphers. Values and sources are in [Data/README.md](Data/README.md#calibrated-login-rates). Re-measure before
changing them.

## Fixture Contract Sync

`Models/SeedModels.cs`, `Seeds/schemas/*.schema.json`, and `Seeds/docs/fixtures.md` describe one contract from three angles — the deserialization target, the editor validation, and the human documentation. A field present in only some of them is silently ignored or silently undocumented: `roster.schema.json` documented a per-user `email` for months while `SeedRosterUser` had no property to carry it.

**Whenever you add, rename, or remove a fixture field:**

- Add the property to the matching `SeedModels.cs` record _and_ the schema, then wire it through the step that consumes it. A schema-only field passes schema validation but is dropped during deserialization
- Update the field's bullet in `Seeds/docs/fixtures.md`, and soften any sibling line the new field makes conditional (an override makes "the Seeder builds emails as X" only true by default)
- Give identifier-bearing strings `"minLength": 1` in the schema, and treat whitespace as absent in the step so it falls back to the derived value rather than committing an unusable row
- Add a row to `Seeds/docs/regression.md` when the field changes what a real seed writes to the database, because the test suite proves the parse, not the seed
- Fixture org domains MUST be `.example` (RFC 2606). Never use `.test`, `.local`, or a real TLD. `Seeds/docs/fixtures.md` holds the full naming table

## Scenarios

Developer-facing documentation in `Seeds/docs/scenarios/`. Each file maps an engineering problem to a Seeder command.

**Maintenance rules:**

- When adding a new preset, check if an existing scenario should reference it as a variation
- When renaming or removing a preset, update the cases in `.claude/skills/seeding-bitwarden-data/evals/evals.json`
- When adding a new command or flag, check if it enables a new scenario or changes an existing one
- When CLI flags, commands, or preset names change, scan all `*.md` files under `Seeds/` and `SeederUtility/` for stale references
- Scenario files follow the template in `Seeds/docs/scenarios/README.md`
- Never duplicate CLI flag documentation; link to `SeederUtility/README.md`
- Never duplicate preset catalog details; link to `Seeds/docs/presets.md`
- Scenarios describe _why_ (the problem). READMEs describe _how_ (the tool). Keep the split clean.
- When commands, flags, entry points, or documentation change in `util/Seeder`, `util/SeederUtility`, or `util/SeederApi`, remind the engineer to review the Seeder page on contributing.bitwarden.com, including its diagram: https://contributing.bitwarden.com/getting-started/server/seeder (source: `docs/getting-started/server/seeder.md` in bitwarden/contributing-docs)
- If that page is stale and the fix is not part of the same change, file a work item and add a stale marker to the page, per the documentation standard

**File relationships:**

- `SeederUtility/README.md` → CLI reference (commands, flags, examples) → links to scenarios
- `Seeds/docs/presets.md` → what exists (the catalog) → scenarios link back to it
- `Seeds/docs/scenarios/` → why you'd use it (problem → command)
- contributing.bitwarden.com › Getting started › Server › Seeder → the higher-level entry point (what the Seeder is, which entry point to use, first run) → links down to `SeederUtility/README.md`, `SeederApi/README.md`, `Seeds/docs/presets.md`, and `Seeds/docs/scenarios/`

## Collection Management Settings

**Collection management settings are not plan-gated.** `AllowAdminAccessToAllCollectionItems`, `LimitCollectionCreation`, `LimitCollectionDeletion`, and `LimitItemDeletion` apply identically across all plan types.
**These settings alter access control behavior.** When seeding scenarios that test member vs. admin permissions, collection creation/deletion policies, or item-level access, set them explicitly in the preset rather than relying on defaults.

**Configurable in presets and CLI.** Use the JSON preset `organization` block (e.g. `"limitCollectionCreation": true`) or the `organization` command flags: `--limit-collection-creation`, `--limit-collection-deletion`, `--limit-item-deletion`, `--allow-admin-collection-access`.

## Dependency Isolation

**Never require a change to shared/production code (e.g. `src/SharedWeb`, `src/Core`) to satisfy a Seeder-only DI need.** The Seeder's DI graph must stay fully self-contained, even at the cost of some duplication. For example, `util/Seeder/Extensions/StripeBillingServiceCollectionExtensions.cs` is the Seeder-owned billing registration both hosts call, and deliberately re-registers `IStripeAdapter`/`IBraintreeGateway` and a subset of Core's billing services rather than extracting a shared helper into `SharedWeb` or `src/Core`.
## Security Reminders

- Default test password: `asdfasdfasdf` (overridable via the `--password` CLI flag)
- Never commit database dumps with seeded data
- Seeded keys are for testing only
