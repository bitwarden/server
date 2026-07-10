---
paths:
  - "**/*.sql"
  - "util/Migrator/**"
  - "src/Infrastructure.Dapper/**"
  - "src/Sql/**"
---

# Dapper / SQL Server migrations & T-SQL style

Bitwarden supports four databases through two ORMs. SQL Server uses Dapper with hand-written stored procedures;
PostgreSQL, MySQL, and SQLite use EF Core. **A schema change must be reflected in both tracks** — after changing
anything here, make the matching EF change (see the `database-ef` rule). Skipping either silently breaks one of the four
supported databases. For architecture, the dual-ORM split, `_V2` stored-procedure versioning, and Evolutionary Database
Design (no-rollback) strategy, defer to the `writing-database-queries` and `implementing-dapper-queries` skills — this
rule covers the deterministic mechanics and T-SQL style.

## SSDT + dated migrations

Every schema change updates **both** the SSDT project (`src/Sql/dbo/`, the current desired state of the database) and a
hand-written dated migration under `util/Migrator/DbScripts/`. Apply locally with `pwsh dev/migrate.ps1`.

Migration filenames are `YYYY-MM-DD_##_Description.sql` and execute in chronological order. (You may see
`DbScripts_transition/` and `DbScripts_finalization/` folders — these are not currently used; ignore them.)

### Migration principles

1. **Idempotent** — a script can run multiple times without erroring. Guard every change with an existence check.
2. **No breaking changes** — never delete or rename a column that is still in use.
3. **Backwards compatible** — the script must work with both the old and new server during a rolling deployment.
4. **Schema integrity** — `src/Sql/dbo/` must always match what a fresh run of all migrations produces.

## T-SQL code style

### File naming (`src/Sql/dbo/`)

- Stored procedures: `{Entity}_{Action}.sql` → `User_Create.sql`, `Organization_ReadById.sql`
- Tables: `{Entity}.sql` → `User.sql` (singular)
- Views: `{Entity}View.sql` (simple) / `{Entity}DetailsView.sql` (complex) → `UserView.sql`
- Functions: `{Entity}{Purpose}.sql` → `UserCollectionDetails.sql`
- Use the `_V2` suffix when maintaining multiple versions during a deployment.

### Formatting

- 4-space indentation, no tabs.
- Keywords UPPERCASE (`CREATE`, `SELECT`, `FROM`, `WHERE`, `INNER JOIN`, `ON`).
- Object names bracketed and schema-qualified: `[dbo].[TableName]`, `[Id]`.
- No space in type modifiers: `NVARCHAR(50)`, not `NVARCHAR (50)`.
- Commas at line end; one column / parameter per line, vertically aligned.
- Use `EXISTS` (with `SELECT 1`) for correlated subqueries, `IN` for non-correlated; prefer `INNER JOIN` on a
  table-valued parameter over a large `IN (...)` list.

### Naming conventions

- Tables singular, PascalCase: `[dbo].[User]` not `[dbo].[Users]`. Columns PascalCase.
- Datetime columns end in `Date` (`CreationDate`, `RevisionDate`), never `CreatedAt`.
- Standard types: `UNIQUEIDENTIFIER` for IDs, `DATETIME2(7)` for timestamps, `NVARCHAR(n)` Unicode / `VARCHAR(n)`
  ASCII, `BIT` for booleans.
- Constraints: `PK_{Table}`, `FK_{Table}_{ReferencedTable}`, `DF_{Table}_{Column}`. Indexes: `IX_{Table}_{Columns}`.
- Stored procedure actions: `Create`, `ReadById`, `ReadBy{Criteria}`, `ReadManyByIds`, `Update`, `DeleteById`. Use
  `Read`/`ReadMany`, never `Get`. Parameters start with `@` in PascalCase.
- Only these user-defined types exist — reuse them, don't invent new ones: `[dbo].[GuidIdArray]`,
  `[dbo].[TwoGuidIdArray]`, `[dbo].[EmailArray]`.

### Stored procedure shape

```sql
CREATE OR ALTER PROCEDURE [dbo].[Entity_Action]
    @Param1 UNIQUEIDENTIFIER,
    @Param2 BIT = NULL
AS
BEGIN
    SET NOCOUNT ON

    -- logic here
END
```

## Migration deployment-script patterns

These guards keep migrations idempotent. Each runnable batch is terminated with `GO`.

### Add a column — guard with `COL_LENGTH` (not `INFORMATION_SCHEMA`)

```sql
IF COL_LENGTH('[dbo].[Table]', 'Column') IS NULL
BEGIN
    ALTER TABLE [dbo].[Table]
        ADD [Column] INT NULL;
END
GO
```

- Add new columns at the **end** of the column list to keep `src/Sql/dbo/` in sync.
- For a `NOT NULL` column, add a `DEFAULT` constraint rather than backfilling rows:
  `ADD [Column] INT NOT NULL CONSTRAINT [DF_Table_Column] DEFAULT 0`. Do **not** use defaults for string columns.

### Create / drop a table

```sql
IF OBJECT_ID('[dbo].[Table]') IS NULL
BEGIN
    CREATE TABLE [dbo].[Table] ( ... )
END
GO

DROP TABLE IF EXISTS [dbo].[Table]
GO
```

### Change a column's data type

```sql
IF EXISTS (
    SELECT *
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'Table'
        AND COLUMN_NAME = 'Column'
        AND DATA_TYPE = 'int')
BEGIN
    ALTER TABLE [dbo].[Table]
        ALTER COLUMN [Column] BIGINT NOT NULL
END
GO
```

### Views, stored procedures, functions — use `CREATE OR ALTER`

```sql
CREATE OR ALTER VIEW [dbo].[EntityView]
AS
SELECT * FROM [dbo].[Entity]
GO
```

After altering a table, refresh dependent views and modules so their metadata is rebuilt:

```sql
EXECUTE sp_refreshview N'[dbo].[EntityView]'
GO

IF OBJECT_ID('[dbo].[Entity_ReadById]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[Entity_ReadById]'
END
GO
```

Drop a procedure/function with `DROP {PROCEDURE|FUNCTION} IF EXISTS [dbo].[Name]`.

### Indexes

Do **not** specify `ONLINE = ON` — production builds indexes online by default. When recreating an existing index, use
`WITH (DROP_EXISTING = ON)`.

```sql
CREATE NONCLUSTERED INDEX [IX_Table_Column]
    ON [dbo].[Table] ([Column] ASC)
    INCLUDE ([OtherColumn])
GO
```
