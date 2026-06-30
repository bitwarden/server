# Bump sdk-internal: Detailed Methodology

## Step-by-Step Process

### 1. Identify the Current Server Pin

```bash
grep 'rev = ' util/RustSdk/rust/Cargo.toml
```

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

That full NPM version string (e.g. `0.2.0-main.522`) is what you map to a git SHA in §3. Do **not**
try to interpret the trailing number — it is an opaque publish counter, not a commit reference.

### 3. Map NPM Version to Git SHA (read it from the published artifact — DETERMINISTIC)

> **Do NOT use timestamps or GitHub Actions run numbers — both are wrong.** The NPM `.NNN`
> suffix is *not* a public GitHub Actions `run_number` (the old "Publish" workflow is gone;
> `build-wasm-internal.yml` only builds and triggers a **private Azure task** to publish, and the
> public build workflow's run numbers are in the thousands while NPM versions are in the hundreds).
> The publish counter also does **not** track `main` commits 1:1 — consecutive NPM versions can
> skip commits or repeat one — so "newest `main` commit near the publish time" picks the WRONG
> commit. (Verified 2026-06-30: `841→c5d5bba`, `842→c9f9dba`, `840→1e45444` — no usable time
> correlation.) The npm packument has no `gitHead` either.

**The build bakes the commit into the artifact.** `build-wasm-internal.yml`'s "Set version" step
embeds `main (${SHA:0:7})` into the published WASM. Read it straight out of the npm tarball:

```bash
# 1. Get the target NPM version from the clients release tag (Step 2 above), e.g. 0.2.0-main.841
# 2. Download that exact published tarball and grep the WASM for the embedded short SHA
cd "$(mktemp -d)"
curl -sSL "https://registry.npmjs.org/@bitwarden/sdk-internal/-/sdk-internal-<VERSION>.tgz" -o pkg.tgz
tar xzf pkg.tgz
grep -ao "main ([0-9a-f]\{7\})" package/bitwarden_wasm_internal_bg.wasm | sort -u
#   -> e.g. "main (c5d5bba)"   <- this short SHA is authoritative

# 3. Resolve the short SHA to the full rev in the sdk-internal checkout
cd /path/to/sdk-internal && git rev-parse c5d5bba
#   -> c5d5bba159bd222321f3ecfd90f5ae6192c2c8eb   <- pin THIS in Cargo.toml
```

That full SHA is the exact source the production client runs — pin it. (`@bitwarden/commercial-sdk-internal`
ships at the same version/commit, so the open-source tarball's embedded SHA is authoritative for both.)

### 4. Verify the Current Pin (for context)

```bash
cd /path/to/sdk-internal
git log --oneline -1 <current-rev>
```

This shows when the current pin was made and what commit it corresponds to.

### 5. Analyze Breaking Changes

List all commits touching `bitwarden-crypto` between the old and new revs:

```bash
cd /path/to/sdk-internal
git log --oneline <old-rev>..<new-rev> -- crates/bitwarden-crypto
```

For each commit, check for:

- **Type renames** (e.g., `AsymmetricCryptoKey` -> `PrivateKey`)
- **Removed or deprecated functions** (look for `#[deprecated]` annotations)
- **Changed function signatures** (parameter types, return types)
- **Trait changes** (new required methods, changed generic bounds)

To check the public API diff:

```bash
git diff <old-rev>..<new-rev> -- crates/bitwarden-crypto/src/keys/mod.rs
git diff <old-rev>..<new-rev> -- crates/bitwarden-crypto/src/lib.rs
```

Cross-reference findings against `references/api-surface.md` to assess impact.

### 6. Apply Code Changes

1. **Cargo.toml** — Update the `bitwarden-crypto` `rev = "..."` to the new SHA
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

**Key validation:** The `encrypt_string_decrypt_string_roundtrip` test proves the new SDK
version correctly encrypts and decrypts data. If this passes, the crypto is working.

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
- **New rev:** `abba7fdab687753268b63248ec22639dff35d07c` (2026-02-05)

> _Historical note:_ this bump mapped the version via a now-removed "Publish" workflow
> run number (ID `126086102`). That method is **deprecated and unreliable** — use the embedded
> `main (<short-sha>)` from the published tarball (§3) instead.

### Breaking Changes Found

| Change                                                | Impact                           | Fix                                      |
| ----------------------------------------------------- | -------------------------------- | ---------------------------------------- |
| `AsymmetricCryptoKey` renamed to `PrivateKey`          | Import + usage in lib.rs         | Rename type                              |
| `AsymmetricPublicCryptoKey` renamed to `PublicKey`     | Import + usage in lib.rs         | Rename type                              |
| `PrivateKey::to_der()` returns `Pkcs8PrivateKeyBytes` | Low risk — auto-refs             | No code change needed                    |
| `encapsulate_key_unsigned` deprecated                  | Deprecation warning              | `#[allow(deprecated)]` + comment         |

### Cargo.lock Review

- All bitwarden-\* crates: `1.0.0` -> `2.0.0` (workspace version bump — expected)
- `coset`: `0.3.8` -> `0.4.1` (COSE library — minor bump)
- New transitive deps: `mockall`, `predicates`, `tracing-attributes` (test/dev deps)
- **No changes to core crypto crates** (rsa, aes, sha2, hmac, pbkdf2)

### Results

- All Rust unit tests passed
- All C# integration tests passed
- NativeMethods.g.cs unchanged
- Human verification: login, vault decryption, seeding all confirmed working

---

## Worked Example: June 2026 Bump

The bump that established the deterministic embedded-SHA mapping (§3), after both the old
run-number method and a timestamp-correlation attempt produced wrong commits.

### Context

- **Old rev:** `abba7fdab687753268b63248ec22639dff35d07c` (2026-02-05, bitwarden-crypto `2.0.0`)
- **Target:** latest production clients (web v2026.6.3; desktop/browser/CLI v2026.6.0)
- **NPM version:** `@bitwarden/sdk-internal` `0.2.0-main.841`
- **New rev:** `c5d5bba159bd222321f3ecfd90f5ae6192c2c8eb` (bitwarden-crypto `3.0.0`)

### SHA mapping (deterministic — embedded in the published WASM)

- `0.2.0-main.841` tarball's `bitwarden_wasm_internal_bg.wasm` embeds `main (c5d5bba)` →
  `git rev-parse c5d5bba` → `c5d5bba159bd222321f3ecfd90f5ae6192c2c8eb`
- **A timestamp heuristic got this wrong** and picked `c9f9dba` (which is actually `0.2.0-main.842`),
  and `0.2.0-main.840` embeds `1e45444` — proof the publish counter does not track commits by time.
  Always read the embedded SHA from the tarball.

### Breaking Changes Found

| Change                                                       | Impact                        | Fix                                              |
| ------------------------------------------------------------ | ----------------------------- | ------------------------------------------------ |
| `SymmetricCryptoKey::make_aes256_cbc_hmac_key()` → `pub(crate)` (#1165) | 5 call sites fail to compile  | Use `make(SymmetricKeyAlgorithm::Aes256CbcHmac)` + import the enum |
| Workspace MSRV `1.85.1` → `1.88.0` (#760, RustCrypto deps)    | `rust-toolchain.toml` too old | Bump `channel` `1.87.0` → `1.88.0`               |

### Cargo.lock Review

- All bitwarden-\* crates: `2.0.0` → `3.0.0` (workspace version bump — expected)
- **Major RustCrypto bumps** (drove the MSRV): `aes` 0.8→0.9, `sha2` 0.10→0.11, `hmac` 0.12→0.13,
  `pbkdf2` 0.12→0.13, `rsa` 0.9→0.10.0-rc, `ed25519-dalek` 2→3.0-rc, `chacha20poly1305` 0.10→0.11-rc,
  `sha1` 0.10→0.11; new `ml-dsa` (post-quantum). Roundtrip test confirmed crypto still correct.
- **New transitive HTTP stack:** `bitwarden-crypto` 3.0.0 now has a *non-optional* dep on
  `bitwarden-api-key-connector` → `bitwarden-api-base` → `reqwest`/`hyper` (key-connector migration,
  #809). Unavoidable from the consumer side; bloats the Seeder build but is functionally harmless.

### Results

- Rust: 13 unit tests passed (incl. `encrypt_string_decrypt_string_roundtrip`)
- C#: 177 SeederApi.IntegrationTest passed (incl. 17 `RustSdkCipherTests`)
- `cargo fmt --check`: clean for changed files (pre-existing `rsa_keys.rs` diff is unrelated)
- NativeMethods.g.cs unchanged
