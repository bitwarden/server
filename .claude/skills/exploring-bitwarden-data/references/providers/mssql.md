# Provider: Microsoft SQL Server

## Environment prerequisites

- `BW_MSSQL_SERVER`
- `BW_MSSQL_DB_NAME`
- `BW_MSSQL_USERNAME`
- `BW_MSSQL_PASSWORD`

```bash
[ -n "${BW_MSSQL_SERVER:-}" ] && [ -n "${BW_MSSQL_DB_NAME:-}" ] && \
[ -n "${BW_MSSQL_USERNAME:-}" ] && [ -n "${BW_MSSQL_PASSWORD:-}" ] && \
echo "Bitwarden MSSQL env vars OK" || echo "MISSING Bitwarden MSSQL env var"
```

## Read-only login

`BW_MSSQL_USERNAME` must be a login that is only in `db_datareader`. The dev default in `dev/secrets.json.example` is `SA`, which can write. Confirm the login with this read-only query before the first query of each session. Run it with the basic tabular pattern below so the columns keep their headers:

```sql
SET NOCOUNT ON;
SELECT
  IS_SRVROLEMEMBER('sysadmin') AS IsSysadmin,
  IS_ROLEMEMBER('db_owner') AS IsDbOwner,
  IS_ROLEMEMBER('db_datawriter') AS IsDataWriter,
  IS_ROLEMEMBER('db_ddladmin') AS IsDdlAdmin,
  IS_ROLEMEMBER('db_datareader') AS IsDataReader,
  HAS_PERMS_BY_NAME(DB_NAME(), 'DATABASE', 'INSERT') AS CanInsert,
  HAS_PERMS_BY_NAME(DB_NAME(), 'DATABASE', 'UPDATE') AS CanUpdate,
  HAS_PERMS_BY_NAME(DB_NAME(), 'DATABASE', 'DELETE') AS CanDelete,
  HAS_PERMS_BY_NAME(DB_NAME(), 'DATABASE', 'EXECUTE') AS CanExecute,
  HAS_PERMS_BY_NAME('dbo', 'SCHEMA', 'ALTER') AS CanAlterDbo;
```

A read-only login returns 1 for `IsDataReader` and 0 everywhere else. If any other column is 1, stop and point the user to the one-time setup below.

### One-time setup (the user runs this as `SA`; never run it from this skill)

Create a reader login once, then point `BW_MSSQL_USERNAME` / `BW_MSSQL_PASSWORD` at it. Replace `<database>` with the database in `BW_MSSQL_DB_NAME`, and repeat the last three lines for each database the skill should read:

```sql
CREATE LOGIN bitwarden_data_reader WITH PASSWORD = '<pick one>';
USE [<database>];
CREATE USER bitwarden_data_reader FOR LOGIN bitwarden_data_reader;
ALTER ROLE db_datareader ADD MEMBER bitwarden_data_reader;
```

## Required tooling

`sqlcmd` (Microsoft Go implementation) must be installed. If absent, stop and surface to the user.

## Dialect notes

Bracket identifiers that collide with reserved keywords — Bitwarden's schema has several: `[Plan]`, `[User]`, `[Group]`, `[Status]`, `[ReadOnly]`. An unbracketed `Plan` or `Group` fails with a syntax error.

## Running queries

Every example below uses `-C -N m -K ReadOnly`: mandatory TLS and certificate trust are required by this environment. `-K ReadOnly` only sets `ApplicationIntent=ReadOnly`, a routing hint for availability-group replicas; on a standalone server it blocks nothing. Keep the flag, but the read-only login is what stops writes. The heredoc form (`-i /dev/stdin`) is the correct way to pass multi-statement SQL to the Go implementation of `sqlcmd` and is not universally obvious.

### Basic tabular

```bash
SQLCMDPASSWORD="$BW_MSSQL_PASSWORD" sqlcmd \
  -S "$BW_MSSQL_SERVER" -U "$BW_MSSQL_USERNAME" -d "$BW_MSSQL_DB_NAME" \
  -C -N m -K ReadOnly \
  -Q "SELECT TOP 10 Id, Name, Seats FROM [dbo].[Organization] ORDER BY Name"
```

### Scalar (use `-h -1` + `-W` + `SET NOCOUNT ON;`)

```bash
SQLCMDPASSWORD="$BW_MSSQL_PASSWORD" sqlcmd \
  -S "$BW_MSSQL_SERVER" -U "$BW_MSSQL_USERNAME" -d "$BW_MSSQL_DB_NAME" \
  -C -N m -K ReadOnly \
  -h -1 -W \
  -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM [dbo].[Cipher]"
```

### Multi-statement / CTE via heredoc

```bash
SQLCMDPASSWORD="$BW_MSSQL_PASSWORD" sqlcmd \
  -S "$BW_MSSQL_SERVER" -U "$BW_MSSQL_USERNAME" -d "$BW_MSSQL_DB_NAME" \
  -C -N m -K ReadOnly \
  -i /dev/stdin <<'SQL'
SET NOCOUNT ON;
WITH MemberCounts AS (
  SELECT OrganizationId, COUNT(*) AS Members
  FROM [dbo].[OrganizationUser]
  WHERE [Status] = 2  -- Confirmed
  GROUP BY OrganizationId
)
SELECT TOP 10 O.Id, O.Name, O.Seats, mc.Members
FROM MemberCounts mc
JOIN [dbo].[Organization] O ON O.Id = mc.OrganizationId
ORDER BY mc.Members DESC;
SQL
```
