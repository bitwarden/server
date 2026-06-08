# Seeded Database Build Pipeline

Builds pre-seeded database Docker images from seeder presets, ready for ephemeral environment deployments.

## Quick Start

```bash
# Build a single preset for postgres (default)
./build-seeded-image.sh qa.dunder-mifflin-enterprise-full

# Build for a specific database type
./build-seeded-image.sh qa.dunder-mifflin-enterprise-full mssql

# Build and push to ACR
PUSH=true ./build-seeded-image.sh qa.dunder-mifflin-enterprise-full postgres

# List all available presets
dotnet run --project ../. -- preset --list --output json
```

## Supported Database Types

| Type | Base Image | Seed Method |
|------|-----------|-------------|
| `postgres` | `postgres:14` | `pg_dump` → init SQL script |
| `mysql` | `mysql:8.0` | `mysqldump` → init SQL script |
| `mariadb` | `mariadb:12` | `mysqldump` → init SQL script |
| `mssql` | `mcr.microsoft.com/mssql/server:2022-CU22-ubuntu-22.04` | MDF/LDF file copy → `CREATE DATABASE ... FOR ATTACH` |
| `sqlite` | `busybox:stable` | Direct `.db` file copy |

`mysql` and `mariadb` share the same migrations project and seeded data; they differ only in the engine the dump is produced from and restored into.

### MSSQL Notes

- MSSQL uses file attach (`CREATE DATABASE ... FOR ATTACH`) instead of `.bak` restore. The `.bak` restore approach fails on Kubernetes PVCs due to `ValidateTargetForCreation` errors — a known issue with MSSQL on certain storage backends.
- The entrypoint waits for all system databases to be ONLINE and verifies the data directory is writable (by creating and dropping a test database) before attempting the attach.
- The database is restored as `vault` (matching the self-host chart's connection string), not `vault_dev` (the seeder's default name).

## Image Tags

Each build produces two tags:

- **Stable**: `seeded-{db}:{preset-name}` — e.g. `seeded-postgres:qa-dunder-mifflin-enterprise-full`
- **Versioned**: `seeded-{db}:{preset-name}-{git-sha}` — e.g. `seeded-postgres:qa-dunder-mifflin-enterprise-full-abc1234`

Images are pushed to `bitwardenprod.azurecr.io/shot/`.

## Traceability

Every seeded image includes two levels of traceability:

**Docker image labels** (`docker inspect`):
```
bitwarden.seeder.preset=qa.dunder-mifflin-enterprise-full
org.opencontainers.image.revision=abc1234
org.opencontainers.image.created=2026-04-16T00:00:00Z
```

**`_SeederMetadata` table in the database**:
```sql
SELECT * FROM "_SeederMetadata";  -- Postgres/MariaDB
SELECT * FROM [_SeederMetadata];  -- MSSQL
```
Returns `preset`, `git_sha`, and `built_at`.

## Data Protection Key

The seeder encrypts certain database fields (e.g. `MasterPassword`, `Key`, `PrivateKey`) using ASP.NET Data Protection. The target deployment environment must have the same key to decrypt these fields.

**Key file**: `key-9aa06f19-9afe-414b-8791-189be3b5650f.xml`

**Known issue**: `ServiceCollectionExtension.cs` does not call `PersistKeysToFileSystem`, so the seeder ignores `appsettings.json`'s `dataProtection.directory` and falls back to `~/.aspnet/DataProtection-Keys/`. The workaround is to copy the key file into `~/.aspnet/DataProtection-Keys/` before running the seeder. The build script handles this automatically.

For CI, set the `DP_KEY_XML` environment variable with the XML content and the script writes it to both locations.

## Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `PUSH` | `false` | Set to `true` to push images to ACR |
| `REGISTRY` | `bitwardenprod.azurecr.io` | ACR registry |
| `GIT_SHA` | Current HEAD | Git SHA for versioned tag |
| `DP_KEY_XML` | (empty) | Data protection key XML content (for CI) |

## GitHub Actions

The workflow at `.github/workflows/build-seeded-databases.yml` supports:

- **Manual dispatch**: Build a single preset + database type. Leave `preset` empty for the curated default list, or set it to `all` to build every preset. Leave `database` as `all` to build the full database matrix.
- **Cron**: Every Sunday at 2am UTC, rebuilds the curated default preset list (`_DEFAULT_PRESETS`) × all database types

The workflow uses a matrix strategy (`preset × database`) with `fail-fast: false`.

## Using Seeded Images in Ephemeral Environments

### Lite chart

```yaml
# values.yaml
database:
  type: postgres  # or mariadb, sqlserver
  image:
    repository: bitwardenprod.azurecr.io/shot/seeded-postgres
    tag: qa-dunder-mifflin-enterprise-full
```

### Self-host chart

```yaml
# values.yaml
self-host:
  database:
    image:
      name: bitwardenprod.azurecr.io/shot/seeded-mssql
      tag: qa-dunder-mifflin-enterprise-full
```

**Note**: Self-host deployments also need the Data Protection key mounted at `/etc/bitwarden/core/aspnet-dataprotection/` for login to work with seeded data.
