---
name: bump-rust-sdk
description: Bump the server's util/RustSdk `bitwarden-crypto` git-rev pin to a bitwarden/clients release — map the client's sdk-internal version to a commit, fix breaking changes, then verify.
when_to_use: When asked to bump the Rust SDK, sdk-internal, or bitwarden-crypto, update RustSdk dependencies, or align the server SDK with clients. Not client-side @bitwarden/sdk-internal npm/package.json bumps.
---

# Bump sdk-internal Rust crate dependencies

## Overview

`util/RustSdk/rust/Cargo.toml` pins `bitwarden-crypto` from `bitwarden/sdk-internal` by git rev; bump
it periodically to track a `bitwarden/clients` release. RustSdk is Seeder **test** infrastructure
(`util/`, not `src/` production): it gives the C# Seeder field-level encryption
(`encrypt_string`/`decrypt_string`/`encrypt_fields`) to produce cryptographically correct Protected
Data for integration tests. `EncryptPropertyAttribute` drives which fields get encrypted.

## NPM → git-rev mapping

Clients consume sdk-internal as npm (`@bitwarden/sdk-internal`, e.g. `0.2.0-main.841`); the server
pins a git rev. The `.NNN` suffix is an opaque counter from a private Azure publish task — **not** a
GitHub Actions `run_number`, and it does not track `main` commits 1:1. Do not infer the SHA from a run
number or timestamp; both pick the wrong commit (verified: `841→c5d5bba`, `842→c9f9dba`, `840→1e45444`
— no time correlation). Read the commit the build bakes into the published WASM instead:

```bash
# <VERSION> is the npm version from the clients release tag (Step 1), e.g. 0.2.0-main.841
cd "$(mktemp -d)"
curl -sSL "https://registry.npmjs.org/@bitwarden/sdk-internal/-/sdk-internal-<VERSION>.tgz" -o pkg.tgz
tar xzf pkg.tgz
grep -ao "main ([0-9a-f]\{7\})" package/bitwarden_wasm_internal_bg.wasm | sort -u   # -> main (c5d5bba)
cd /path/to/sdk-internal && git rev-parse c5d5bba   # -> full rev to pin in Cargo.toml
```

That full SHA is the exact source the production client runs. `@bitwarden/commercial-sdk-internal`
ships the same commit, so the open-source tarball's embedded SHA is authoritative for both.

## Process

1. **Identify target** — latest `web-v*` release tag from clients, then its npm version:
   ```bash
   gh release list --repo bitwarden/clients --limit 5 | grep web-v
   git -C /path/to/clients show <tag>:package.json | grep sdk-internal   # -> 0.2.0-main.841
   ```
2. **Map npm → git SHA** — the mapping section above.
3. **Analyze breaking changes** — cross-ref each commit against `references/api-surface.md`; watch for
   type renames, removed/deprecated functions, changed signatures, and trait changes:
   ```bash
   cd /path/to/sdk-internal
   git log --oneline <old-rev>..<new-rev> -- crates/bitwarden-crypto
   git diff <old-rev>..<new-rev> -- crates/bitwarden-crypto/src/keys/mod.rs crates/bitwarden-crypto/src/lib.rs
   ```
4. **Apply** —
   - Update the `bitwarden-crypto` `rev` in `Cargo.toml`.
   - **MSRV check:** compare the target's workspace `rust-version` against `util/RustSdk/rust-toolchain.toml`;
     if the MSRV rose above our channel, bump the channel to the **MSRV** (not sdk-internal's dev
     toolchain), else the build fails "requires rustc X or newer".
   - `cargo update -p bitwarden-crypto` (targeted — re-resolves only what the new rev needs; a bare
     `cargo update` churns the lockfile).
   - Fix compile errors; add `#[allow(deprecated)]` + a why-comment for newly-deprecated APIs.
5. **Build & verify (Claude)** —
   ```bash
   cd util/RustSdk/rust
   cargo build && cargo test        # roundtrip test is the gate: encrypt_string_decrypt_string_roundtrip
   cargo fmt --check
   git diff ../NativeMethods.g.cs   # must be unchanged
   dotnet test test/SeederApi.IntegrationTest/
   ```
   Review the `Cargo.lock` diff for unexpected transitive crypto crates (`rsa`, `aes`, `sha2`).
6. **Human verification (human only — present these, do not run them)** — the human seeds and confirms
   Protected Data is decryptable in the web client:
   ```bash
   cd util/SeederUtility
   dotnet run -- organization -n SdkBumpTest -d sdk-bump-test.example -u 3 -c 10 -g 5 -o Traditional -m
   # or a preset:  dotnet run -- seed --preset dunder-mifflin-enterprise-full --mangle

   # SeederApi HTTP alternative — start `dotnet run` in util/SeederApi, then:
   curl -X POST localhost:5000/seed -H 'X-Play-Id: sdk-bump-test' -H 'Content-Type: application/json' \
     -d '{"template":"SingleUserScene","arguments":{"email":"test@example.com","password":"<8+ char pwd>"}}'
   curl -X DELETE localhost:5000/seed/sdk-bump-test   # cleanup
   ```
   Pass = seeded users log in with the fake master password (see `util/SeederUtility/README.md`),
   ciphers are decryptable in the web vault, no Seeder errors, and cleanup deletes all tracked entities.

## Security notes

- RustSdk is `util/` test infrastructure, not `src/` production; the server never decrypts Vault Data,
  so the zero-knowledge invariant is unaffected.
- Matching the production client release makes the Seeder produce Protected Data with the same
  cryptographic primitives real clients use.

## Keeping api-surface.md current

`references/api-surface.md` must mirror the actual code. After a bump, regenerate it: read every `.rs`
in `util/RustSdk/rust/src/`, extract the `bitwarden_crypto` `use` statements, and rewrite the file.
A Stop hook (`.claude/hooks/rust-sdk-surface-check.sh`) blocks if `Cargo.toml` changed but
`api-surface.md` did not.

## Files typically modified

| File | Change |
| --- | --- |
| `util/RustSdk/rust/Cargo.toml` | `bitwarden-crypto` rev pin |
| `util/RustSdk/rust-toolchain.toml` | bump `channel` only if the MSRV rose |
| `util/RustSdk/rust/src/*.rs` | breaking-change fixes |
| `util/RustSdk/rust/Cargo.lock` | re-resolved (`cargo update -p bitwarden-crypto`) |
| `util/RustSdk/NativeMethods.g.cs` | should NOT change — verify |

## References

- `references/api-surface.md` — types, traits, and functions imported from `bitwarden-crypto`.
- `references/examples/2026-06-bump.md` — the June 2026 bump, worked end-to-end.
