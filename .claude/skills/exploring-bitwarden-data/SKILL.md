---
name: exploring-bitwarden-data
description: Read-only exploration of a local Bitwarden development database — answer business questions from live data, verify seeded fixtures, and introspect schema. Use whenever the user wants to query, count, look up, verify, or explore data in a local Bitwarden database ("how many orgs/users/ciphers", "show me collections", "check what the seeder created", "look up user X", "which orgs have feature Y"), even without the word SQL. Not for authoring stored procedures, migrations, or repository code (use writing-database-queries), and not for seeding or modifying data.
argument-hint: "[mssql|mysql|postgresql|sqlite] <question or SQL>"
user-invocable: true
allowed-tools: "Bash(which sqlcmd), Bash(sqlcmd:*)"
hooks:
  PreToolUse:
    - matcher: "Bash"
      hooks:
        - type: command
          command: "bash ${CLAUDE_PROJECT_DIR}/.claude/hooks/block-mutating-sql.sh"
          timeout: 5
---

# Explorer Bitwarden Database

Read-only access to a local Bitwarden database. MSSQL is wired today; the other providers are scaffolds.

## Read-only, defense in depth

1. Database login is read-only at the server — mutations will fail regardless of what you send.
2. PreToolUse hook blocks non-read SQL at the bash boundary.
3. Allowed: `SELECT`, `WITH` (CTEs), and `INFORMATION_SCHEMA` / `sys.*` introspection.

## Cross-provider rules

- **Secrets.** Never echo, log, `cat`, `printenv`, or `hexdump` any password env var. Set passwords inline on the command (e.g., `SQLCMDPASSWORD="$BW_MSSQL_PASSWORD" sqlcmd ...`); never `export` them.
- **Result presentation.** Format <20 rows as a markdown table; summarize larger sets as top-N + count. Always echo the SQL ran. Trim CLI footers (`(N rows affected)`, `Query OK`) before presenting.
- **Heredoc footgun.** Use single-quoted heredoc tags (`<<'SQL'`) — without quotes, bash expands `$` inside the SQL before the database CLI sees it, breaking column references.

## Provider selection

First arg picks the provider — `mssql` (default), `mysql`, `postgresql`, or `sqlite`. Read the matching provider reference before composing SQL.

| Provider   | Env prefix    | CLI       | Reference                                                                | Status    |
| ---------- | ------------- | --------- | ------------------------------------------------------------------------ | --------- |
| MSSQL      | `BW_MSSQL_*`  | `sqlcmd`  | [references/providers/mssql.md](references/providers/mssql.md)           | **Ready** |
| MySQL      | `BW_MYSQL_*`  | `mysql`   | [references/providers/mysql.md](references/providers/mysql.md)           | Stub      |
| PostgreSQL | `BW_PG_*`     | `psql`    | [references/providers/postgresql.md](references/providers/postgresql.md) | Stub      |
| SQLite     | `BW_SQLITE_*` | `sqlite3` | [references/providers/sqlite.md](references/providers/sqlite.md)         | Stub      |

## The repo is the schema's source of truth

Don't compose SQL from a generic mental model of how a vault schema "probably" looks — and don't expect this skill to inventory the schema for you. The repo already does, and it stays current when this file wouldn't:

- **Tables and columns**: SSDT schema under `src/Sql/dbo/`, or live introspection via [references/schema-discovery-queries.md](references/schema-discovery-queries.md).
- **Enum integer values and lifecycle semantics**: the C# enum sources — their XML docs carry meaning no value table can (seat consumption, restore behavior, deprecations).
- **Access-control logic**: prefer the canonical functions over hand-rolled joins — `[dbo].[UserCipherDetails](@UserId)` for "what can user X see", `[dbo].[UserCollectionDetails](@UserId)` for collection permissions. They encode member status, org enablement, and direct-over-group grant precedence that is easy to rebuild subtly wrong.
- **Where to look**: [references/sources.md](references/sources.md) maps every concept named in this skill to its source file.

## Grounding rules

Semantics the schema itself cannot tell you — each of these flipped a real eval case that unaided Claude got wrong (evidence in `evals/baseline-results.md`; that is also the bar for adding a rule here).

1. **Active member = `OrganizationUser.Status = 2` (Confirmed).** "Active" is genuinely ambiguous — the occupied-seat definition (`Status IN (0,1,2)`, used by the seat-count procs) is a defensible rival reading, so state which one the question needs. Full lifecycle (including Staged and Revoked-with-restore) is documented in `OrganizationUserStatusType.cs`.
2. **Archive state lives in `Cipher.Archives` — per-user JSON keyed by UPPERCASE user GUID — not in the `ArchivedDate` column.** `ArchivedDate` exists on the table but the archive flow never writes it (`Cipher_Archive` does `JSON_MODIFY` on `Archives`); querying it returns zero forever while looking perfectly reasonable. `Favorites` and `Folders` use the same per-user JSON shape, so interpolate keys from a `UNIQUEIDENTIFIER` (SQL Server renders them uppercase; JSON keys are case-sensitive).
3. **`Organization.Enabled = 1` is the active flag.** `Organization.Status` is the provider-management lifecycle (Pending/Created/Managed), and `Plan` is a display string — aggregate and filter on `PlanType`.

## Reference library

| Reference                                                                        | When to read                                                              |
| -------------------------------------------------------------------------------- | ------------------------------------------------------------------------- |
| [references/sources.md](references/sources.md)                                   | Finding the source file for any table, enum, or canonical function        |
| [references/schema-discovery-queries.md](references/schema-discovery-queries.md) | Live introspection — list tables, describe columns, find FKs, view bodies |
| [references/providers/mssql.md](references/providers/mssql.md)                   | MSSQL connection, sqlcmd invocation patterns, dialect notes               |
