---
name: exploring-bitwarden-data
description: Read-only exploration of a local Bitwarden development database — answer business questions from live data, verify seeded fixtures, and introspect schema. Use whenever the user wants to query, count, look up, verify, or explore data in a local Bitwarden database ("how many orgs/users/ciphers", "show me collections", "check what the seeder created", "look up user X", "which orgs have feature Y"), even without the word SQL. Not for authoring stored procedures, migrations, or repository code (use writing-database-queries), and not for seeding or modifying data.
argument-hint: "[mssql|mysql|postgresql] <question or SQL>"
user-invocable: true
allowed-tools: "Bash(which sqlcmd), Bash(which mysql), Bash(which psql), Bash(sqlcmd:*), Bash(mysql:*), Bash(psql:*)"
---

# Explore Bitwarden Database

Read-only access to a local Bitwarden database, across all three dev providers.

## Read-only, defense in depth

1. The login in `BW_*_USERNAME` must be read-only at the server. Writes fail because of that login's grants, not because of any connection flag. Each provider reference has a query that confirms the login is read-only. Run it before the session's first query, and if it shows any write access, stop and tell the user.
2. Allowed: `SELECT`, `WITH` (CTEs), `INFORMATION_SCHEMA` / `sys.*` introspection, and the session `SET` statements the provider references require (`SET NOCOUNT ON`, `SET TEXTSIZE`, `SET SESSION TRANSACTION READ ONLY`, `SET default_transaction_read_only = on`).

## Cross-provider rules

- **Secrets.** Never echo, log, `cat`, `printenv`, or `hexdump` any password env var. Set passwords inline on the command (e.g., `SQLCMDPASSWORD="$BW_MSSQL_PASSWORD" sqlcmd ...`); never `export` them.
- **Result presentation.** Format <20 rows as a markdown table; summarize larger sets as top-N + count. Always echo the SQL ran. Trim CLI footers (`(N rows affected)`, `Query OK`) before presenting.
- **Heredoc footgun.** Use single-quoted heredoc tags (`<<'SQL'`) — without quotes, bash expands `$` inside the SQL before the database CLI sees it, breaking column references.

## Provider selection

First arg picks the provider — `mssql` (default), `mysql`, or `postgresql`. Read the matching provider reference before composing SQL.

| Provider   | Env prefix      | CLI      | Reference                                                                | Status    |
| ---------- | --------------- | -------- | ------------------------------------------------------------------------ | --------- |
| MSSQL      | `BW_MSSQL_*`    | `sqlcmd` | [references/providers/mssql.md](references/providers/mssql.md)           | **Ready** |
| MySQL      | `BW_MYSQL_*`    | `mysql`  | [references/providers/mysql.md](references/providers/mysql.md)           | **Ready** |
| PostgreSQL | `BW_POSTGRES_*` | `psql`   | [references/providers/postgresql.md](references/providers/postgresql.md) | **Ready** |

## The repo is the schema's source of truth

Don't compose SQL from a generic mental model of how a vault schema "probably" looks — and don't expect this skill to inventory the schema for you. The repo already does, and it stays current when this file wouldn't:

- **Tables and columns**: SSDT schema under `src/Sql/dbo/`, or live introspection via [references/schema-discovery-queries.md](references/schema-discovery-queries.md) (MSSQL only). On MySQL, `information_schema` lists the tables under `TABLE_SCHEMA = DATABASE()`; on PostgreSQL they are in schema `public`.
- **Enum integer values and lifecycle semantics**: the C# enum sources — their XML docs carry meaning no value table can (seat consumption, restore behavior, deprecations).
- **Access-control logic** (MSSQL only; on MySQL and PostgreSQL rebuild it from the function sources in [references/sources.md](references/sources.md)): prefer the canonical functions over hand-rolled joins — `[dbo].[UserCipherDetails](@UserId)` for "what can user X see", `[dbo].[UserCollectionDetails](@UserId)` for collection permissions. They encode member status, org enablement, and direct-over-group grant precedence that is easy to rebuild subtly wrong. Both return one row per grant, and a direct grant on a collection hides that collection's group grants. A cipher in two collections, or a collection granted through two groups, therefore appears twice: count `DISTINCT Id`. To merge permissions per `Id`, use `MAX(Edit)`, `MAX(ViewPassword)`, `MAX(Manage)` on `UserCipherDetails` (as `CipherDetails_ReadByIdUserId` does) and `MIN(ReadOnly)`, `MIN(HidePasswords)`, `MAX(Manage)` on `UserCollectionDetails` (as `Collection_ReadByUserId` does).
- **Where to look**: [references/sources.md](references/sources.md) maps every concept named in this skill to its source file.

## Grounding rules

Semantics the schema itself cannot tell you. Add a rule here only when it flips a case in [evals/evals.json](evals/evals.json) that unaided Claude gets wrong.

1. **Active member = `OrganizationUser.Status = 2` (Confirmed).** "Active" is genuinely ambiguous — the occupied-seat definition (`Status IN (0,1,2)` plus admin-initiated sponsorships, used by the seat-count procs) is a defensible rival reading, so state which one the question needs. Full lifecycle (including Staged and Revoked-with-restore) is documented in `OrganizationUserStatusType.cs`.
2. **Archive state lives in `Cipher.Archives` — per-user JSON keyed by user GUID — not in the `ArchivedDate` column.** `ArchivedDate` exists on the table but the archive flow never writes it (`Cipher_Archive` does `JSON_MODIFY` on `Archives`); querying it returns zero forever while looking perfectly reasonable. `Favorites` and `Folders` use the same per-user JSON shape. JSON keys are case-sensitive. On SQL Server they are uppercase, so interpolate keys from a `UNIQUEIDENTIFIER`, which SQL Server renders uppercase. On MySQL and PostgreSQL they can be either case (the EF repositories write lowercase, the Seeder uppercase), so check both as the provider reference shows.
3. **`Organization.Enabled = 1` is the active flag.** `Organization.Status` is the provider-management lifecycle (Pending/Created/Managed), and `Plan` is a display string — aggregate and filter on `PlanType`.

## Reference library

| Reference                                                                        | When to read                                                              |
| -------------------------------------------------------------------------------- | ------------------------------------------------------------------------- |
| [references/sources.md](references/sources.md)                                   | Finding the source file for any table, enum, or canonical function        |
| [references/schema-discovery-queries.md](references/schema-discovery-queries.md) | Live introspection — list tables, describe columns, find FKs, view bodies |
| [references/providers/mssql.md](references/providers/mssql.md)                   | MSSQL connection, sqlcmd invocation patterns, dialect notes               |
| [references/providers/mysql.md](references/providers/mysql.md)                   | MySQL connection, mysql invocation patterns, dialect notes                |
| [references/providers/postgresql.md](references/providers/postgresql.md)         | PostgreSQL connection, psql invocation patterns, dialect notes            |
