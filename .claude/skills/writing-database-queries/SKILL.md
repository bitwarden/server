---
name: writing-database-queries
description: Bitwarden database architecture, migrations, and dual-ORM strategy. Use when working with `.sql` files, stored procedures, EF migrations, or database schema changes. Also use when deciding whether a change needs both Dapper and EF Core implementations, or whether a breaking stored-procedure change requires `_V2` versioning.
---

## Dual-ORM Architecture

Bitwarden maintains two data access implementations, split by database provider:

- **MSSQL:** Dapper with stored procedures
- **PostgreSQL, MySQL, SQLite:** Entity Framework Core

These implementations are **mutually exclusive at runtime** — SQL Server uses only Dapper, while the other providers use only EF Core. Both implementations conform to the same repository interfaces.

- When **adding new repository functionality**, implement it in **both** Dapper and EF Core (unless the feature is explicitly EF-only).
- When **modifying an existing stored procedure** in a backwards-compatible way (for example, adding a new parameter with a default), **EF Core changes are not required**.
- Some commercial features (for example, **Secrets Manager**) are **EF Core only**.

## Evolutionary Database Design (EDD)

Bitwarden Cloud uses a **no-rollback** approach to database deployments. The key implication: **server deployments can be rolled back, but database migrations cannot**, so migrations must be designed to avoid being a source of downtime.

All MSSQL migrations live in `util/Migrator/DbScripts/` and execute in chronological order based on the migration filename (`YYYY-MM-DD_##_Description.sql`).

> Note: You may see `util/Migrator/DbScripts_transition/` and `util/Migrator/DbScripts_finalization/` folders. These are not currently used; ignore them for now.

Simple additive changes (new nullable column, new table, new stored procedure) typically require only a single migration script in `util/Migrator/DbScripts/`.

### Stored procedure compatibility

Stored procedure changes fall into two categories:

- **Non-breaking (DEFAULT parameters):** Adding a parameter with a default value (e.g., `@NewParam BIT = NULL`) is backwards-compatible. Existing callers keep working; no `_V2` is needed.
- **Breaking (`_V2` versioning):** Required when result-set structure changes, calling patterns change (e.g., single result → multiple result sets), required parameters are added without defaults, or query semantics differ. Implement this by creating `ProcedureName_V2` while retaining the original procedure for backwards compatibility.

Table-level breaking changes (removing columns, changing types) typically cascade into stored procedure changes and often require the `_V2` pattern.

**Always defer to the developer on migration strategy.** The approach is complex and context-dependent. When a database change is needed, write the migration script and ask the developer whether `_V2` versioning or additional steps are required.

## Key locations

- `src/Sql/dbo` — Master schema source of truth
- `util/Migrator/DbScripts` — All migrations (single folder, chronological)

## ORM-Specific Implementation

When implementing Dapper repository methods, stored procedures, or MSSQL migration scripts, activate the `implementing-dapper-queries` skill.

When implementing EF Core repositories, generating EF migrations, or working with PostgreSQL/MySQL/SQLite, activate the `implementing-ef-core` skill.

## Critical Rules

These are the most frequently violated conventions. Claude cannot fetch the linked docs at runtime, so these are inlined here:

- **Migration file naming:** `YYYY-MM-DD_##_Description.sql` (e.g., `2025-06-15_00_AddVaultColumn.sql`)
- **All schema objects use `dbo` schema** — never create objects in other schemas
- **Constraint naming:** `PK_TableName` (primary key), `FK_Child_Parent` (foreign key), `IX_Table_Column` (index), `DF_Table_Column` (default)
- **Idempotent scripts:** Use `IF NOT EXISTS` / `IF COL_LENGTH(...)` guards before schema changes in migration scripts
- **New repository functionality requires both Dapper and EF Core implementations** — unless the feature is explicitly EF-only or the change is a backwards-compatible stored procedure modification
- **Integration tests use `[DatabaseData]` attribute** — this runs the test against all configured database providers

## Further Reading

- [SQL code style](https://contributing.bitwarden.com/contributing/code-style/sql/)
- [Database migrations (MSSQL)](https://contributing.bitwarden.com/contributing/database-migrations/mssql)
- [Database migrations (EF)](https://contributing.bitwarden.com/contributing/database-migrations/ef)
- [Evolutionary Database Design](https://contributing.bitwarden.com/contributing/database-migrations/edd)
