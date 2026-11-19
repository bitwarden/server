---
name: seeding-bitwarden-data
description: Seed a local Bitwarden development database with the Seeder CLI. Picks an existing preset before building a custom org or user, runs the command, and lists the logins it created. Use whenever someone needs test data to log into or test against, even without the word seed ("seed my DB", "I need an org with N users", "give me a premium user", "fresh database to log into", "reproduce a customer's org shape", "org with collection permission edge cases"). DO NOT use for querying or verifying seeded data (use exploring-bitwarden-data), changing Seeder code, or the SeederApi HTTP service.
argument-hint: "<the data you need, in plain words>"
allowed-tools: "Bash(dotnet run --project util/SeederUtility:*)"
---

# Seed Bitwarden data

Turn a plain-English request into one Seeder CLI run, then list the logins it created. Assume the workstation is configured.

## Rules

- Never read or print the Seeder's configuration: user secrets, environment variables, connection strings.
- Never reset, clear, or delete data, and never offer to.
- Ask before any `--stripe-billing` run.
- If a run fails, say it failed and stop. Don't diagnose, retry, or investigate.

## 1. Ask the Seeder

The CLI is the source of truth for presets and flags. Run from the repo root, and give every `dotnet run` a 10-minute timeout (the first build compiles the Rust SDK):

```bash
dotnet run --project util/SeederUtility -- preset --list
dotnet run --project util/SeederUtility -- <command> --help
```

## 2. Select a preset

Presets are reviewed, reproducible data. Match the request to one through `util/Seeder/Seeds/docs/scenarios/README.md`.
Use `organization` or `individual` only when no preset fits.
If neither a preset nor a custom command fits, offer the closest preset and say what it lacks.

## 3. Run

Tell the user you are invoking the Seeder and show the command, then run it:

```bash
dotnet run --project util/SeederUtility -- <command> [options] --mangle
```

Pass `--mangle` unless the user wants exact emails. Read that from their wording: they name an email to log in with (pass it through `--owner-email`, or for `individual`, `--email` with `--first-name` and `--last-name`), say the database is fresh, reset, or empty, or ask for plain, real, or unprefixed logins. When the wording is unclear, mangle; don't ask.

If the permission system blocks the run, give the user the command to run with `! <command>`.

## 4. Report

Finish with a table of the logins, password, org ID, and counts the CLI printed, with emails exactly as printed. For `dev.playground`, the role logins are in `util/Seeder/Seeds/docs/presets.md`.
