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

## Required tooling

`sqlcmd` (Microsoft Go implementation) must be installed. If absent, stop and surface to the user.

## Dialect notes

Bracket identifiers that collide with reserved keywords — Bitwarden's schema has several: `[Plan]`, `[User]`, `[Group]`, `[Status]`, `[ReadOnly]`. An unbracketed `Plan` or `Group` fails with a syntax error.

## Running queries

Every example below uses `-C -N m -K ReadOnly`: mandatory TLS and certificate trust are required by this environment, and `-K ReadOnly` enforces the read-only connection intent at the driver level — a safety constraint that is not part of standard `sqlcmd` invocations Claude would construct by default. The heredoc form (`-i /dev/stdin`) is the correct way to pass multi-statement SQL to the Go implementation of `sqlcmd` and is not universally obvious.

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
