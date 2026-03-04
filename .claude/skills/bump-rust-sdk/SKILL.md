---
name: bump-rust-sdk
description: This skill should be used when the user asks to "bump the Rust SDK", "update sdk-internal", "bump bitwarden-crypto", "update RustSdk dependencies", "align server SDK with clients", or needs to update the bitwarden/sdk-internal git rev pins in util/RustSdk/rust/Cargo.toml. Provides the methodology for mapping client NPM versions to git commit SHAs, analyzing breaking changes, auditing the API surface, and verifying the bump end-to-end.
---

# Bump sdk-internal Rust Crate Dependencies

## Overview

The server's `util/RustSdk/rust/Cargo.toml` pins three crates from the `bitwarden/sdk-internal`
repository by git rev: `bitwarden-core`, `bitwarden-crypto`, and `bitwarden-vault`. These must
be periodically bumped to stay aligned with the Bitwarden client applications.

The RustSdk is used by the Seeder to produce cryptographically correct Protected Data for
integration testing. It is NOT part of the production server runtime.

## Key Challenge: NPM-to-Git-Rev Mapping

The clients consume sdk-internal via **NPM packages** (`@bitwarden/sdk-internal`), while the
server consumes it via **Rust git rev pins**. The NPM version (e.g., `0.2.0-main.522`) does not
directly correspond to a git tag — it encodes a GitHub Actions **workflow run number**.

### Version Format

```
0.2.0-main.522
│     │     │
│     │     └── GitHub Actions run number for publish-wasm-internal workflow
│     └── Branch name (/ replaced with -)
└── Base version from sdk-internal
```

### How to Find the Git Rev

1. Determine the target NPM version from the clients repo (see Step 1 below)
2. Find the `Publish @bitwarden/sdk-internal` workflow ID in the sdk-internal repo
3. Query the GitHub Actions API for the specific run number
4. Extract the `head_sha` — that is the git rev to pin in Cargo.toml

The specific API queries are documented in `references/methodology.md`.

## Process Overview

### Step 1: Identify Target Version

Determine which sdk-internal version to target. Check the latest production release tag from
`bitwarden/clients` (e.g., `web-v2026.2.0`):

```bash
cd /path/to/clients
git show web-v2026.2.0:package.json | grep sdk-internal
```

This gives the NPM version (e.g., `0.2.0-main.522`). Extract the run number (522).

### Step 2: Map NPM Version to Git SHA

Query the GitHub Actions API to find the commit that produced that NPM build. See
`references/methodology.md` for the exact commands.

### Step 3: Analyze Breaking Changes

Compare the current pinned rev against the target rev, focusing on the three crates:

```bash
cd /path/to/sdk-internal
git log --oneline <old-rev>..<new-rev> -- crates/bitwarden-core crates/bitwarden-crypto crates/bitwarden-vault
```

Cross-reference each commit against the API surface documented in `references/api-surface.md`.

### Step 4: Apply Changes

1. Update `Cargo.toml` — bump all three rev pins to the same SHA
2. Fix any compilation errors from breaking changes (type renames, new struct fields, etc.)
3. Add `#[allow(deprecated)]` for any newly-deprecated APIs (with a comment explaining why)

### Step 5: Build and Verify (Claude)

```bash
cd util/RustSdk/rust
cargo build                # Must compile cleanly
cargo test                 # All tests must pass (roundtrip test is critical)
cargo fmt --check          # Formatting must be clean
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
from the three bitwarden crates, and rewrite `references/api-surface.md` to match.

## Additional Resources

### Reference Files

- **`references/methodology.md`** — Detailed step-by-step commands including GitHub Actions API
  queries, breaking change analysis checklist, human verification commands, and a worked example
  from the Feb 2026 bump
- **`references/api-surface.md`** — Complete inventory of types, traits, and functions the RustSdk
  imports from each crate, used to assess breaking change impact

## Files Modified in a Typical Bump

| File                              | Change                                             |
| --------------------------------- | -------------------------------------------------- |
| `util/RustSdk/rust/Cargo.toml`    | Rev pin update                                     |
| `util/RustSdk/rust/src/*.rs`      | Type renames, new struct fields, deprecation fixes |
| `util/RustSdk/rust/Cargo.lock`    | Auto-regenerated (commit alongside)                |
| `util/RustSdk/NativeMethods.g.cs` | Should NOT change (verify)                         |
