---
name: bump-rust-sdk
description: This skill should be used when the user asks to "bump the Rust SDK", "update sdk-internal", "bump bitwarden-crypto", "update RustSdk dependencies", "align server SDK with clients", or needs to update the bitwarden/sdk-internal git rev pins in util/RustSdk/rust/Cargo.toml. Provides the methodology for mapping client NPM versions to git commit SHAs, analyzing breaking changes, auditing the API surface, and verifying the bump end-to-end.
---

# Bump sdk-internal Rust Crate Dependencies

## Overview

The server's `util/RustSdk/rust/Cargo.toml` pins `bitwarden-crypto` from the
`bitwarden/sdk-internal` repository by git rev. This must be periodically bumped to stay
aligned with the Bitwarden client applications.

The RustSdk is used by the Seeder to produce cryptographically correct Protected Data for
integration testing. It is NOT part of the production server runtime. The Rust layer provides
generic field-level encryption (`encrypt_string`, `decrypt_string`, `encrypt_fields`) and
key generation — the C# Seeder drives which fields to encrypt via `EncryptPropertyAttribute`.

## Key Challenge: NPM-to-Git-Rev Mapping

The clients consume sdk-internal via **NPM packages** (`@bitwarden/sdk-internal`), while the
server consumes it via **Rust git rev pins**. The NPM version (e.g., `0.2.0-main.841`) does not
correspond to a git tag, and the `.NNN` suffix is **not** a public GitHub Actions run number —
it is a counter from a private Azure publish pipeline. Map it by reading the commit baked into
the published WASM (`main (<short-sha>)`) — see the deterministic method below.

### Version Format

```
0.2.0-main.841
│     │     │
│     │     └── Opaque publish counter from a private Azure task — NOT a GitHub Actions
│     │         run_number, and it does NOT track main commits 1:1. Do not infer the SHA from it.
│     └── Branch name (/ replaced with -)
└── Base version from sdk-internal
```

### How to Find the Git Rev (deterministic)

The build bakes the commit into the published WASM as `main (<short-sha>)`. Read it directly —
do not guess from run numbers or timestamps:

1. Determine the target NPM version from the clients repo (see Step 1 below)
2. Download that exact version's tarball from the npm registry
3. `grep` `bitwarden_wasm_internal_bg.wasm` for `main (<short-sha>)`
4. `git rev-parse <short-sha>` in sdk-internal → the full rev to pin in Cargo.toml

The exact commands (and why timestamp/run_number methods are wrong) are in `references/methodology.md` §3.

## Process Overview

### Step 1: Identify Target Version

Determine which sdk-internal version to target. Check the latest production release tag from
`bitwarden/clients` (e.g., `web-v2026.2.0`):

```bash
cd /path/to/clients
git show web-v2026.2.0:package.json | grep sdk-internal
```

This gives the NPM version (e.g., `0.2.0-main.841`).

### Step 2: Map NPM Version to Git SHA

Download that NPM version's tarball and read the `main (<short-sha>)` string embedded in
`bitwarden_wasm_internal_bg.wasm`, then `git rev-parse` it. This is deterministic — see
`references/methodology.md` §3 for the exact commands.

### Step 3: Analyze Breaking Changes

Compare the current pinned rev against the target rev, focusing on `bitwarden-crypto`:

```bash
cd /path/to/sdk-internal
git log --oneline <old-rev>..<new-rev> -- crates/bitwarden-crypto
```

Cross-reference each commit against the API surface documented in `references/api-surface.md`.

### Step 4: Apply Changes

1. Update `Cargo.toml` — bump the `bitwarden-crypto` rev pin to the new SHA
2. **Check the MSRV.** Compare the target's workspace `rust-version` (root `Cargo.toml`,
   inherited by `bitwarden-crypto` via `rust-version.workspace = true`) against
   `util/RustSdk/rust-toolchain.toml`. If the MSRV rose above our pinned channel, bump the
   channel to match (match the **MSRV**, not sdk-internal's dev toolchain). Skipping this
   yields a "requires rustc X or newer" build failure.
3. Run `cargo update -p bitwarden-crypto` to re-resolve `Cargo.lock` for the new rev. The
   targeted form updates only what the new rev requires (still fixing stale transitive pins
   like `hybrid-array` that would otherwise cause resolution conflicts); a bare `cargo update`
   also works but churns unrelated crates and needlessly bloats the lockfile diff.
4. Fix any compilation errors from breaking changes (type renames, new parameters, etc.)
5. Add `#[allow(deprecated)]` for any newly-deprecated APIs (with a comment explaining why)

### Step 5: Build and Verify (Claude)

```bash
cd util/RustSdk/rust
cargo build                # Must compile cleanly
cargo test                 # All tests must pass (roundtrip test is critical)
cargo fmt --check          # Changed files must be clean
git diff ../NativeMethods.g.cs  # FFI signatures should be unchanged
```

Also run the C# integration tests:

```bash
dotnet test test/SeederApi.IntegrationTest/
```

### Step 6: Human Verification (HUMAN ONLY)

**Claude does NOT perform this step.** Present these commands to the human engineer and wait
for confirmation before proceeding.

The human runs SeederUtility and SeederApi to verify Protected Data is correctly produced and
decryptable by the web client. See `references/methodology.md` for the specific test commands
and validation criteria.

## Security Notes

- The RustSdk lives in `util/` (test infrastructure), not `src/` (production)
- The server never decrypts Vault Data — zero-knowledge invariant is unaffected
- Aligning with the production client release ensures the Seeder produces Protected Data
  using the same cryptographic primitives as real clients
- Review `Cargo.lock` diff for unexpected transitive crypto crate changes (rsa, aes, sha2, etc.)

## Keeping References Current

The API surface reference (`references/api-surface.md`) must always reflect the actual code.
Two mechanisms enforce this:

1. **Post-bump step** — The `/bump-rust-sdk` command includes a mandatory final step to
   regenerate `api-surface.md` by reading the actual `*.rs` source files.
2. **Stop hook** — `.claude/hooks/rust-sdk-surface-check.sh` blocks if `Cargo.toml` was
   modified but `api-surface.md` was not updated in the same session.

To regenerate: read all `.rs` files in `util/RustSdk/rust/src/`, extract every `use` statement
from `bitwarden_crypto`, and rewrite `references/api-surface.md` to match.

## Additional Resources

### Reference Files

- **`references/methodology.md`** — Detailed step-by-step commands including the deterministic
  embedded-WASM `main (<short-sha>)` → git-rev mapping, breaking change analysis checklist, human
  verification commands, and worked examples from the Feb 2026 and June 2026 bumps
- **`references/api-surface.md`** — Complete inventory of types, traits, and functions the RustSdk
  imports from `bitwarden-crypto`, used to assess breaking change impact

## Files Modified in a Typical Bump

| File                              | Change                                          |
| --------------------------------- | ----------------------------------------------- |
| `util/RustSdk/rust/Cargo.toml`    | `bitwarden-crypto` rev pin update                       |
| `util/RustSdk/rust-toolchain.toml`| Bump `channel` if sdk-internal raised its MSRV (`rust-version`) |
| `util/RustSdk/rust/src/*.rs`      | Type renames, new parameters, deprecation fixes         |
| `util/RustSdk/rust/Cargo.lock`    | Re-resolved (`cargo update -p bitwarden-crypto`; commit alongside) |
| `util/RustSdk/NativeMethods.g.cs` | Should NOT change (verify)                              |
