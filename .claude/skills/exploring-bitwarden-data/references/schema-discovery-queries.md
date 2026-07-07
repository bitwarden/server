# Schema Discovery Queries — Bitwarden (MSSQL)

Read-only `INFORMATION_SCHEMA` and `sys.*` queries for tables, columns, FKs, and view definitions. MSSQL-only — other providers will use their own introspection (MySQL `INFORMATION_SCHEMA`, PostgreSQL `pg_catalog`).

Read when the SSDT sources mapped in [sources.md](sources.md) aren't enough: full schema enumeration, tables or columns not listed there, or fetching a live view's definition.

Wrap each query below in the standard sqlcmd invocation from [providers/mssql.md](providers/mssql.md). `{{SCHEMA_NAME}}` is almost always `dbo`.

## List all tables and views in a schema

```sql
SELECT TABLE_SCHEMA, TABLE_NAME, 'TABLE' AS ObjectType
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_SCHEMA = '{{SCHEMA_NAME}}' AND TABLE_TYPE = 'BASE TABLE'
UNION ALL
SELECT TABLE_SCHEMA, TABLE_NAME, 'VIEW' AS ObjectType
FROM INFORMATION_SCHEMA.VIEWS
WHERE TABLE_SCHEMA = '{{SCHEMA_NAME}}'
ORDER BY ObjectType, TABLE_NAME;
```

## Search for columns by name across a schema

```sql
SELECT TABLE_SCHEMA, TABLE_NAME, COLUMN_NAME, DATA_TYPE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = '{{SCHEMA_NAME}}' AND COLUMN_NAME LIKE '%{{SEARCH_TERM}}%'
ORDER BY TABLE_NAME, COLUMN_NAME;
```

## Describe a table's columns

```sql
SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE, COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = '{{SCHEMA_NAME}}' AND TABLE_NAME = '{{TABLE_NAME}}'
ORDER BY ORDINAL_POSITION;
```

## Get a view's definition

`SET TEXTSIZE` is bumped because the default 4 KB will truncate larger views.

```sql
SET TEXTSIZE 1000000;
SELECT OBJECT_DEFINITION(OBJECT_ID('{{SCHEMA_NAME}}.{{VIEW_NAME}}')) AS Definition;
```

## Find foreign-key relationships for a table

The `OBJECT_SCHEMA_NAME(...)` calls disambiguate same-named tables across schemas.

```sql
SELECT
    fk.name AS FK_Name,
    OBJECT_SCHEMA_NAME(fk.parent_object_id)      AS ParentSchema,
    tp.name AS ParentTable,
    cp.name AS ParentColumn,
    OBJECT_SCHEMA_NAME(fk.referenced_object_id)  AS ReferencedSchema,
    tr.name AS ReferencedTable,
    cr.name AS ReferencedColumn
FROM sys.foreign_keys fk
JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
JOIN sys.tables  tp ON fkc.parent_object_id      = tp.object_id
JOIN sys.columns cp ON fkc.parent_object_id      = cp.object_id AND fkc.parent_column_id     = cp.column_id
JOIN sys.tables  tr ON fkc.referenced_object_id  = tr.object_id
JOIN sys.columns cr ON fkc.referenced_object_id  = cr.object_id AND fkc.referenced_column_id = cr.column_id
WHERE tp.name = '{{TABLE_NAME}}' OR tr.name = '{{TABLE_NAME}}'
ORDER BY fk.name;
```
