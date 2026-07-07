# Provider: PostgreSQL

Local dev: compose service `postgres` (container `bitwardenserver-postgres-1`), host port 5432, database `vault_dev`, user `postgres`.

## Environment prerequisites

Look for these before any connection work; `BW_POSTGRES_USERNAME` points at the read-only login (`bitwarden_data_reader`), not the `postgres` superuser:

- `BW_POSTGRES_SERVER`
- `BW_POSTGRES_DB_NAME`
- `BW_POSTGRES_USERNAME`
- `BW_POSTGRES_PASSWORD`

```bash
[ -n "${BW_POSTGRES_SERVER:-}" ] && [ -n "${BW_POSTGRES_DB_NAME:-}" ] && \
[ -n "${BW_POSTGRES_USERNAME:-}" ] && [ -n "${BW_POSTGRES_PASSWORD:-}" ] && \
echo "Bitwarden PostgreSQL env vars OK" || echo "MISSING Bitwarden PostgreSQL env var"
```

## Read-only guard — required on every connection

Connect as the read-only login in `BW_POSTGRES_USERNAME` (`bitwarden_data_reader`, `SELECT`-only). The bash hook only inspects `sqlcmd`, so it won't catch `psql` writes — as defense-in-depth, also open every session with `SET default_transaction_read_only = on;` (verified: writes then fail with `cannot execute INSERT in a read-only transaction`). This guard is per-session, so it only holds if the statement is actually issued.

## Connecting

Prefer host `psql` with the env vars above when installed; otherwise go through the container. Either form connects as `BW_POSTGRES_USERNAME` and keeps the `SET` guard.

Host `psql` (when installed):

```bash
PGPASSWORD="$BW_POSTGRES_PASSWORD" psql \
  -h "$BW_POSTGRES_SERVER" -U "$BW_POSTGRES_USERNAME" -d "$BW_POSTGRES_DB_NAME" -At \
  -c "SET default_transaction_read_only = on;" \
  -c "SELECT COUNT(*) FROM \"Organization\""
```

Container fallback (host `psql` not installed — connects as the reader over the trusted local socket):

```bash
docker exec -i bitwardenserver-postgres-1 \
  psql -U "$BW_POSTGRES_USERNAME" -d "$BW_POSTGRES_DB_NAME" -At \
  -c "SET default_transaction_read_only = on;" \
  -c "SELECT COUNT(*) FROM \"Organization\""
```

Multi-statement / CTE via heredoc (`-i` on docker exec is required — without it stdin is not attached and output is silently empty):

```bash
docker exec -i bitwardenserver-postgres-1 psql -U "$BW_POSTGRES_USERNAME" -d "$BW_POSTGRES_DB_NAME" -At <<'PSQL'
SET default_transaction_read_only = on;
SELECT o."Name", COUNT(ou."Id") AS members
FROM "Organization" o
LEFT JOIN "OrganizationUser" ou ON ou."OrganizationId" = o."Id" AND ou."Status" = 2
GROUP BY o."Name" ORDER BY members DESC LIMIT 10;
PSQL
```

## Dialect notes (vs the MSSQL examples)

- **Quote every PascalCase identifier** (`"OrganizationUser"`, `"Status"`) — EF created them case-sensitive; unquoted names fold to lowercase and fail. This is the #1 error source when translating from the MSSQL reference.
- No `[brackets]`, no `TOP n` (use `LIMIT n`), no `GETUTCDATE()` (use `now() AT TIME ZONE 'utc'`), no `WITH (NOLOCK)`.
- String concat is `||`. The `Archives`/`Favorites`/`Folders` per-user JSON columns are `text` — use `col::jsonb ->> 'UPPERCASE-GUID'`.
- The canonical MSSQL TVFs (`UserCipherDetails`, `UserCollectionDetails`) do not exist — EF providers have no stored procedures or functions. Rebuild their logic from the function sources in [../sources.md](../sources.md).
