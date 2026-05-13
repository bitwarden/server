---
description: Bump sdk-internal Rust crate dependencies in util/RustSdk to align with a Bitwarden clients release
argument-hint: [clients-release-tag]
allowed-tools: Read, Write, Edit, Glob, Grep, Bash(cargo *), Bash(dotnet *), Bash(git *), Bash(gh *)
---

Bump the sdk-internal Rust crate dependencies in `util/RustSdk/rust/Cargo.toml` to align with
the Bitwarden clients production release specified by `$ARGUMENTS`. If no release tag is given,
determine the latest `web-v*` release tag from `bitwarden/clients`.

Invoke the `bump-rust-sdk` skill using the task tool for the full methodology, API surface reference, and worked examples.

## Required Context

Before starting, read these files to understand the current state:

- `util/RustSdk/rust/Cargo.toml` — current rev pins
- `util/RustSdk/rust/src/*.rs` — current API usage
- `.claude/skills/bump-rust-sdk/references/api-surface.md` — documented API surface
- `.claude/skills/bump-rust-sdk/references/methodology.md` — detailed process

## Execution

Follow the skill's process in order:

1. **Identify target** — Find the `@bitwarden/sdk-internal` NPM version at the release tag
2. **Map to git SHA** — Query the GitHub Actions API for the publish workflow run number
3. **Analyze breaking changes** — Compare old rev to new rev across the three crates, using the API surface reference
4. **Apply changes** — Update Cargo.toml, fix compilation errors, handle deprecations
5. **Build and verify** — `cargo build`, `cargo test`, `dotnet test test/SeederApi.IntegrationTest/`, `cargo fmt --check`
6. **Human verification** — Present the SeederUtility and SeederApi test commands to the human. **Do NOT run these yourself.** Wait for the human to confirm.
7. **Regenerate API surface** — Read all `.rs` files in `util/RustSdk/rust/src/` and update `.claude/skills/bump-rust-sdk/references/api-surface.md` to reflect the current imports, types, and traits. This step is mandatory — the reference must always match the actual code.

## Important Rules

- All three crate rev pins MUST be the same SHA
- Do NOT make unrelated formatting or style changes to the Rust source files
- Do NOT run SeederUtility or SeederApi yourself — the human performs all end-to-end testing
- Do NOT skip the API surface regeneration step — a Stop hook will block if it is missed
- `util/RustSdk/NativeMethods.g.cs` should NOT change — verify with `git diff` after build
- `util/RustSdk/rust/Cargo.lock` will change and must be included alongside the other changes
