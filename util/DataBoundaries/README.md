# Data Boundaries

Maps database tables and columns to the code and team that own them, and finds code that reads or writes data owned by another service boundary.

## Why

Bitwarden is being decomposed into coarse services. The blocker is our data and our data access patterns. We have 'god-like' services and repositories across the `src/Core/Core.csproj` with too many purposes and cross-domain boundary access violations. Every one of those accesses has to be resolved before the owning service can move its data behind an interface.

The Data Boundaries tool aids people in resolving the problem by walking our code and measuring the cross-service / cross-team domain boundary violations.

The schema is inherited. Columns accumulated on shared tables over many years, and most crossings predate everyone now working on them. Team ownership enters for one reason: routing the work to the team best suited to refactor.

## Input sources

1. SSDT DDL under `src/Sql/dbo/**/Tables/*.sql` for the authoritative column list.
2. `.github/CODEOWNERS` for the owning team, applying GitHub's last-matching-rule-wins.
3. C# source, through Roslyn, for column reads and writes.

**It has no database access and no connection-string option. Do not add one.** The column list has to be reproducible on any checkout and in CI, which a live query is not. Output carries table names, column names, repo-relative paths, and team handles. Never row data, never vault data.

## Tool execution

```bash
# Write the schema and ownership map to generated/schema-inventory.json
dotnet run --project util/DataBoundaries -- scan

# Inspect one table. Prints to stdout and does not write the shared map
dotnet run --project util/DataBoundaries -- scan --table Organization

# Enforce the rules. 0 clean, 1 policy violation, 2 tool failure
dotnet run --project util/DataBoundaries -- check

# Prove Roslyn can load the solution and resolve a known column read
# Rewrites unrelated packages.lock.json files as a side effect; discard those before committing
dotnet run --project util/DataBoundaries -- roslyn-smoke

# Point the same walk at a different entity. Prints every column's reads and writes instead of
# just the oracle's; skips the oracle/cascade/scope checks, which only mean something for the
# Organization/MaxStorageGb default.
dotnet run --project util/DataBoundaries -- roslyn-smoke --type Bit.Core.Entities.User --property Key --max-writes 1000 --max-reads 2000
```

`check --sql-ownership` fails a changed table file that resolves to no team or to `@bitwarden/dept-dbops` alone, unless it is listed in `config/grandfathered-tables.txt`. `--staleness` fails when the committed map no longer matches a fresh scan. CI passes `--changed-files` and `--baseline-allowlist`; without a baseline, allowlist growth is reported as unchecked rather than silently passed.

Output is deterministic. Two runs are byte-identical, with no timestamps, absolute paths, or CRLF, so a regenerated map can be diffed to detect drift.

## Every table needs a product owner

A table needs the product code that owns its data. `@bitwarden/dept-dbops` owns the database platform, not what the rows mean.

A table file in a domain folder such as `src/Sql/dbo/Vault/Tables/` resolves to the product team, because the later `**/Vault` rule beats the earlier `src/Sql/**` rule. A table file in the unclassified root `src/Sql/dbo/Tables/` resolves to dbops and so has no product code accountable for it. 27 files are still in that root.

That gives the goal a countable form: empty out `src/Sql/dbo/Tables/`. The grandfathered list may shrink and must never grow. Moving the file is the whole ownership change, so do not add a redundant CODEOWNERS rule alongside it. The list also covers 11 domain-folder table files whose ownership resolves to `@bitwarden/dept-dbops` alone: the 4 under `src/Sql/dbo/Pam/Tables/`, kept with dbops by a deliberate, commented rule near the end of CODEOWNERS, and the 7 under `src/Sql/dbo/SecretsManager/Tables/`, where no Secrets Manager rule exists anywhere in the file and no `team-secrets-manager` handle exists to point at. Both are pending decisions rather than accidents.

## Attribution is by symbol, not by text

Text matching cannot tell `user.Id` from `organization.Id`, which is the reason this uses Roslyn at all.

It also deliberately avoids `SymbolFinder.FindReferencesAsync`. That API cascades to interface members, and both `Organization` and `User` implement `IStorable.MaxStorageGb`, so a search for one table's property returns the other table's call sites. Measured here: 102 of 178 results belonged to the wrong table. Comparing `IPropertyReferenceOperation.Property.ContainingType` against the entity keeps them apart.

Reads hidden behind entity helper methods are attributed transitively. `Organization.StorageBytesRemaining()` reads `Storage` and `MaxStorageGb` while its callers name neither, and `DisplayName()` reads `Name` from 26 files. The attribution names the entity helper the consumer called, so it can be audited rather than taken on trust.

## Reporting limitations

Check `csharpAnalysis` in the generated map before trusting it. While it reads `not-performed-stage-1`, the file carries schema and ownership only and no consumer data at all.

Dapper and stored procedures are invisible to a Roslyn pass. Column use inside `src/Sql` procedures and views needs a separate DDL sweep, and `SecretsManagerBeta` is the worked example: no C# reference, still carried by every procedure that gets recreated.

Indirect access is also invisible. Code reading `OrganizationAbility` or a response DTO never names the entity property, so it does not appear as a consumer.

SSDT and the live database can disagree. An earlier pass counted 674 columns from a live `INFORMATION_SCHEMA` query, while SSDT yields 671 across 62 tables. SSDT is what the tool reads, so reconcile before treating either as authoritative.
