# Provider: MySQL

Local dev: compose service `mysql` (container `bitwardenserver-mysql-1`), host port 3306, database `vault_dev`, user `root`.

## Environment prerequisites

Look for these before any connection work; `BW_MYSQL_USERNAME` points at the read-only user (`bitwarden_data_reader`), not `root`:

- `BW_MYSQL_SERVER`
- `BW_MYSQL_DB_NAME`
- `BW_MYSQL_USERNAME`
- `BW_MYSQL_PASSWORD`

```bash
[ -n "${BW_MYSQL_SERVER:-}" ] && [ -n "${BW_MYSQL_DB_NAME:-}" ] && \
[ -n "${BW_MYSQL_USERNAME:-}" ] && [ -n "${BW_MYSQL_PASSWORD:-}" ] && \
echo "Bitwarden MySQL env vars OK" || echo "MISSING Bitwarden MySQL env var"
```

## Read-only guard — required on every connection

Connect as the read-only user in `BW_MYSQL_USERNAME` (`bitwarden_data_reader`, `SELECT`-only). The bash hook only inspects `sqlcmd`, so it won't catch `mysql` writes — as defense-in-depth, also open every session with `SET SESSION TRANSACTION READ ONLY;` (verified: writes then fail with `ERROR 1792: Cannot execute statement in a READ ONLY transaction`; autocommit statements are implicit transactions, so plain INSERTs are covered). This guard is per-session, so it only holds if the statement is actually issued.

## Connecting

Prefer host `mysql` with the env vars above when installed; otherwise go through the container. Either form connects as `BW_MYSQL_USERNAME` and keeps the `SET` guard. Pass the password via `MYSQL_PWD` (never `-p`, which prints a warning). The client is **silent on success and prints errors to stderr** — don't blanket-suppress stderr, or failures vanish.

Host `mysql` (when installed):

```bash
MYSQL_PWD="$BW_MYSQL_PASSWORD" mysql \
  -h "$BW_MYSQL_SERVER" -u"$BW_MYSQL_USERNAME" "$BW_MYSQL_DB_NAME" \
  -e "SET SESSION TRANSACTION READ ONLY; SELECT COUNT(*) FROM Organization;"
```

Container fallback (host `mysql` not installed — inject the password into the container; the `@'%'` reader matches the socket, so add `-h 127.0.0.1` only if a socket connection is refused):

```bash
docker exec -e MYSQL_PWD="$BW_MYSQL_PASSWORD" -i bitwardenserver-mysql-1 \
  mysql -u"$BW_MYSQL_USERNAME" "$BW_MYSQL_DB_NAME" \
  -e "SET SESSION TRANSACTION READ ONLY; SELECT COUNT(*) FROM Organization;"
```

Multi-statement via heredoc (`-i` on docker exec is required — without it stdin is not attached and output is silently empty):

```bash
docker exec -e MYSQL_PWD="$BW_MYSQL_PASSWORD" -i bitwardenserver-mysql-1 \
  mysql -u"$BW_MYSQL_USERNAME" "$BW_MYSQL_DB_NAME" <<'MYSQL'
SET SESSION TRANSACTION READ ONLY;
SELECT Name, Seats FROM Organization ORDER BY Name LIMIT 10;
MYSQL
```

## Dialect notes (vs the MSSQL examples)

- PascalCase table/column names work unquoted on the Linux containers, but **`Group` is a reserved word — backtick it** (`` `Group` ``). No `[brackets]`.
- No `TOP n` (use `LIMIT n`), no `GETUTCDATE()` (use `UTC_TIMESTAMP()`), no `WITH (NOLOCK)`.
- The `Archives`/`Favorites`/`Folders` per-user JSON columns are `longtext` — use `JSON_UNQUOTE(JSON_EXTRACT(col, '$."UPPERCASE-GUID"'))`.
- The canonical MSSQL TVFs (`UserCipherDetails`, `UserCollectionDetails`) do not exist — EF providers have no stored procedures or functions. Rebuild their logic from the function sources in [../sources.md](../sources.md).
