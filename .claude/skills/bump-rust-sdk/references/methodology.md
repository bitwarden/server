# Bump sdk-internal: Detailed Methodology

## Step-by-Step Process

### 1. Identify the Current Server Pin

```bash
grep 'rev = ' util/RustSdk/rust/Cargo.toml
```

All three crates (`bitwarden-core`, `bitwarden-crypto`, `bitwarden-vault`) must always pin to
the **same rev**. If they don't, something is wrong.

### 2. Identify the Target Version from Clients

Determine the latest production release tag from the clients repo:

```bash
# Check latest web release
gh release list --repo bitwarden/clients --limit 5 | grep web-v

# Get the sdk-internal NPM version at that tag
cd /path/to/clients
git show <tag>:package.json | grep sdk-internal
```

Example output: `"@bitwarden/sdk-internal": "0.2.0-main.522"`

The run number is **522** — the last segment after the branch name.

### 3. Map NPM Run Number to Git SHA

The NPM version encodes a GitHub Actions workflow run number, not a git tag. Query the API:

```bash
# Find the publish workflow ID
gh api "repos/bitwarden/sdk-internal/actions/workflows" \
  --jq '.workflows[] | "\(.id) \(.name)"' | grep -i "Publish.*sdk-internal"

# Query for the specific run number (replace WORKFLOW_ID and RUN_NUMBER)
gh api "repos/bitwarden/sdk-internal/actions/workflows/WORKFLOW_ID/runs?per_page=100" \
  --jq '.workflow_runs[] | select(.run_number == RUN_NUMBER) | "\(.run_number) \(.head_sha) \(.created_at) \(.head_branch)"'
```

The `head_sha` in the output is the git commit to pin in Cargo.toml.

**If the run is older than 100 runs ago**, paginate:

```bash
gh api "repos/bitwarden/sdk-internal/actions/workflows/WORKFLOW_ID/runs?per_page=100&page=2" \
  --jq '.workflow_runs[] | select(.run_number == RUN_NUMBER) | ...'
```

### 4. Verify the Current Pin (for context)

```bash
cd /path/to/sdk-internal
git log --oneline -1 <current-rev>
```

This shows when the current pin was made and what commit it corresponds to.

### 5. Analyze Breaking Changes

List all commits touching the three crates between the old and new revs:

```bash
cd /path/to/sdk-internal
git log --oneline <old-rev>..<new-rev> -- \
  crates/bitwarden-core crates/bitwarden-crypto crates/bitwarden-vault
```

For each commit, check for:

- **Type renames** (e.g., `AsymmetricCryptoKey` -> `PrivateKey`)
- **New required struct fields** (e.g., new `Option<T>` fields on `CipherView`)
- **Removed or deprecated functions** (look for `#[deprecated]` annotations)
- **Changed function signatures** (parameter types, return types)
- **Trait changes** (new required methods, changed generic bounds)

To check the public API diff for a specific crate:

```bash
git diff <old-rev>..<new-rev> -- crates/bitwarden-crypto/src/keys/mod.rs
git diff <old-rev>..<new-rev> -- crates/bitwarden-crypto/src/lib.rs
git diff <old-rev>..<new-rev> -- crates/bitwarden-vault/src/cipher/cipher.rs
```

Cross-reference findings against `references/api-surface.md` to assess impact.

### 6. Apply Code Changes

1. **Cargo.toml** — Update all three `rev = "..."` to the new SHA
2. **Rust source files** — Fix compilation errors from breaking changes
3. **Deprecation warnings** — Add `#[allow(deprecated)]` with a comment explaining why
4. Do NOT make unrelated formatting or style changes

### 7. Build and Test (Claude)

```bash
cd util/RustSdk/rust

# Compile
cargo build

# Review transitive dependency changes (focus on crypto crates)
git diff Cargo.lock | grep "^[+-]name\|^[+-]version" | head -40

# Verify FFI signatures unchanged
git diff ../NativeMethods.g.cs

# Run Rust tests (roundtrip test is critical)
cargo test

# Run C# integration tests
dotnet test test/SeederApi.IntegrationTest/

# Format checks
cargo fmt --check
```

**Key validation:** The `encrypt_decrypt_roundtrip_preserves_plaintext` test proves the new SDK
version correctly encrypts and decrypts Vault Data. If this passes, the crypto is working.

### 8. Human Verification (HUMAN ONLY — Claude does NOT run these)

Present these commands to the human engineer. Wait for confirmation before proceeding.

**SeederUtility — seed an org with vault data:**

```bash
cd util/SeederUtility
dotnet run -- organization -n SdkBumpTest -d sdk-bump-test.example -u 3 -c 10 -g 5 -o Traditional -m
```

**SeederUtility — seed a fixture preset:**

```bash
dotnet run -- seed --preset dunder-mifflin-enterprise-full --mangle
```

**SeederApi — start and seed via HTTP:**

You need to replace the empty password argument with at least an 8-character master password for the fake user account

```bash
cd util/SeederApi
dotnet run
# In another terminal:
curl -X POST http://localhost:5000/seed \
  -H "Content-Type: application/json" \
  -H "X-Play-Id: sdk-bump-test" \
  -d '{"template": "SingleUserScene", "arguments": {"email": "test@example.com", "password": ""}}'
```

**SeederApi — cleanup:**

```bash
curl -X DELETE http://localhost:5000/seed/sdk-bump-test
```

**Validation criteria (human checks):**

- Seeded users can log in to the web vault with the fake master password
  - See `util/SeederUtility/README.md` for the default master password used by Seeder
- Vault Data (ciphers) is visible and decryptable in the web client
- No errors in SeederUtility or SeederApi output
- SeederApi cleanup deletes all tracked entities

---

## Worked Example: February 2026 Bump

This section documents the actual bump performed in Feb 2026 as a reference.

### Context

- **Old rev:** `7080159154a42b59028ccb9f5af62bf087e565f9` (2025-11-20)
- **Target:** `web-v2026.2.0` production release
- **NPM version:** `@bitwarden/sdk-internal` `0.2.0-main.522`
- **Workflow ID:** `126086102` (Publish @bitwarden/sdk-internal)
- **New rev:** `abba7fdab687753268b63248ec22639dff35d07c` (2026-02-05)

### Breaking Changes Found

| Change                                                    | Impact                                   | Fix                                        |
| --------------------------------------------------------- | ---------------------------------------- | ------------------------------------------ |
| `AsymmetricCryptoKey` renamed to `PrivateKey`             | Import + usage in lib.rs                 | Rename type                                |
| `AsymmetricPublicCryptoKey` renamed to `PublicKey`        | Import + usage in lib.rs                 | Rename type                                |
| `CipherView` added `attachment_decryption_failures` field | Test struct literal in cipher.rs         | Add `attachment_decryption_failures: None` |
| `PrivateKey::to_der()` returns `Pkcs8PrivateKeyBytes`     | Low risk — auto-refs to `KeyEncryptable` | No code change needed                      |
| `encapsulate_key_unsigned` deprecated                     | Deprecation warning                      | `#[allow(deprecated)]` + comment           |

### Cargo.lock Review

- All bitwarden-\* crates: `1.0.0` -> `2.0.0` (workspace version bump — expected)
- `coset`: `0.3.8` -> `0.4.1` (COSE library — minor bump)
- New transitive deps: `mockall`, `predicates`, `tracing-attributes` (test/dev deps)
- **No changes to core crypto crates** (rsa, aes, sha2, hmac, pbkdf2)

### Results

- 7/7 Rust unit tests passed
- 65/65 C# integration tests passed
- NativeMethods.g.cs unchanged
- Human verification: login, vault decryption, seeding all confirmed working
