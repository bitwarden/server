---
name: querying-bitwarden-database
description: Read-only query access to a local Bitwarden database (MSSQL primary; MySQL/PostgreSQL/SQLite reference scaffolds in place).
when-to-use: Use when phrases include "query the Bitwarden database", "run SQL against Bitwarden", "look up data in Bitwarden", "explore the Bitwarden schema", "query Cipher records", "query Organization data", "look up vault items", "how many users", "show me collections", "what orgs have feature X", or any variation of querying, exploring, or inspecting Bitwarden data.
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

# Bitwarden Database Reader

Read-only access to a local Bitwarden database, grounded in Bitwarden's actual schema, enums, and access-control semantics. Provider-pluggable: MSSQL is wired today, the others are scaffolds.

Bitwarden's schema encodes business and security meaning, not just storage — ownership, membership, access policy, and lifecycle state all live in how these tables relate. Grasping what the data _represents_ is what turns a business-language question into a correct query.

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

## Bitwarden grounding rules

Provider-agnostic Bitwarden domain facts that contradict assumptions an LLM would otherwise make from public training data. Source citations in [references/sources.md](references/sources.md).

1. **Active member = `OrganizationUser.Status = 2`** (Confirmed). `Status` is `SMALLINT` — full values in [references/enums.md](references/enums.md).
2. **Active cipher = `Cipher.DeletedDate IS NULL`.** No `IsDeleted` bit.
3. **`Cipher.UserId XOR OrganizationId` is enforced in code only** — no CHECK constraint. Personal-vault filters must include `OrganizationId IS NULL`.
4. **Use `[dbo].[UserCipherDetails](@UserId)` for "what ciphers can user X see"** — the canonical TVF unions personal + direct + group-mediated access with the `Organization.Enabled = 1` gate.
5. **`CollectionUser.OrganizationUserId` joins to `OrganizationUser.Id`, not `User.Id`.** Same for `GroupUser`:
   ```sql
   CollectionUser CU
   JOIN OrganizationUser OU ON CU.OrganizationUserId = OU.Id
   JOIN [User] U             ON OU.UserId             = U.Id
   ```
6. **Permission resolution = `COALESCE(CollectionUser.x, CollectionGroup.x, 0)`** — direct user grants beat group grants. `Edit = NOT ReadOnly`, `ViewPassword = NOT HidePasswords`, `Manage` standalone.
7. **`Cipher.Favorites`, `.Folders`, `.Archives` are per-user JSON keyed by UPPERCASE GUID.** SQL Server interpolates `UNIQUEIDENTIFIER` as uppercase; JSON keys are case-sensitive. Interpolate from a `UNIQUEIDENTIFIER` variable:
   ```sql
   JSON_VALUE(Favorites, CONCAT('$."', @UserId, '"'))
   ```
8. **`Organization.Plan` is a display string; `PlanType` (TINYINT) is the enum.** `Organization.Enabled = 1` is the active flag; `Organization.Status` is the provider-management lifecycle (Pending/Created/Managed).
9. **Avoid `SELECT *` on Cipher/Send/User/Organization; never `LIKE` on encrypted columns.** Non-obvious encrypted columns: `Folder.Name` and `Collection.Name` look like plain strings but are AES256-CBC ciphertext. `Cipher.Data`, `Send.Data`/`Key`, `User.Key`/`MasterPassword`/`PrivateKey` are also opaque — null-check or length-check only. Conversely, `User.Email`, `User.Name`, `Organization.Name`, and `Group.Name` are plaintext — searchable with `LIKE`/`=`. (`Group.Name` is **not** encrypted: `GroupRequestModel.Name` is a `[StringLength(100)]` plain string, unlike the `[EncryptedStringLength]` on Collection/Folder names — only Collection and Folder names are encrypted.)
10. **`Send` has three dates, a disabled bit, and an access-count cap — all must compose.** A live Send satisfies: `DeletionDate > GETUTCDATE()`, `ExpirationDate IS NULL OR ExpirationDate > GETUTCDATE()`, `Disabled = 0`, and `MaxAccessCount IS NULL OR AccessCount < MaxAccessCount`. Filtering on only one date silently over-counts available Sends.
11. **`AuthRequest` has no `ExpirationDate` column — expiry is a runtime offset from `CreationDate`.** Pending requests: `ResponseDate IS NULL AND CreationDate > DATEADD(SECOND, -900, GETUTCDATE())`. Do not attempt to filter on a non-existent expiry field.

## Reference Library

These references are bundled so the always-loaded skill stays lean — pull in only what a question needs, when you need it. **DO NOT** compose SQL from a generic mental model of how a schema "probably" looks: Bitwarden's tables encode domain rules that public training data gets wrong, so guessing yields a query that runs cleanly but answers the wrong question. Before anything past a trivial single-table lookup, open the reference that matches what you're reasoning about — entity choice, joins, or reusing a view.

| Reference                                                                        | When to read                                                                                                                            |
| -------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------- |
| [references/schema-tables.md](references/schema-tables.md)                       | Choosing the right starting entity and confirming it means what you assume (names mislead); which mandatory filters a valid query needs |
| [references/schema-relationships.md](references/schema-relationships.md)         | Composing any multi-table join — cardinality, the `OrganizationUser`-not-`User` pitfall, `UserId` XOR `OrganizationId`                  |
| [references/schema-views.md](references/schema-views.md)                         | Before hand-rolling joins — a view may already compute the answer; also which views' filtering has drifted                              |
| [references/sources.md](references/sources.md)                                   | Master registry of every source file cited in this skill                                                                                |
| [references/enums.md](references/enums.md)                                       | Integer values for `Status`, `Type`, `PolicyType`, `RevocationReason`, etc.                                                             |
| [references/schema-discovery-queries.md](references/schema-discovery-queries.md) | Introspection — list tables, find FKs, fetch a view definition                                                                          |
| [references/providers/mssql.md](references/providers/mssql.md)                   | MSSQL connection, sqlcmd flags, MSSQL syntax glossary                                                                                   |
| [references/providers/mysql.md](references/providers/mysql.md)                   | (Stub)                                                                                                                                  |
| [references/providers/postgresql.md](references/providers/postgresql.md)         | (Stub)                                                                                                                                  |
| [references/providers/sqlite.md](references/providers/sqlite.md)                 | (Stub)                                                                                                                                  |
