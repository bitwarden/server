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
dotnet run --project .. -- preset --list --output json
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

Traceability lives entirely in the Docker image labels (`docker inspect`):

```
bitwarden.seeder.preset=qa.dunder-mifflin-enterprise-full
bitwarden.seeder.category=qa
org.opencontainers.image.revision=abc1234
org.opencontainers.image.created=2026-04-16T00:00:00Z
```

The category is derived from the preset name prefix, which matches the fixture folder under `Seeds/fixtures/presets/`.

## Core Bundle

Two things the seeder produces are read by the **application**, not the database, so they cannot travel inside the database image:

- **Data protection keys** — the seeder encrypts some fields (`MasterPassword`, `Key`, `PrivateKey`) with ASP.NET Data Protection. Without the same key, logins fail.
- **Attachment blobs** — the encrypted file bodies. The database only holds attachment metadata.

Both live under `/etc/bitwarden/core` in a deployment, so each build writes them to one tarball next to the image:

```
docker/bundles/seeded-core-{preset}-{git-sha}.tar.gz
└── core/
    ├── aspnet-dataprotection/key-….xml
    └── attachments/{cipherId}/{attachmentId}
```

Set `DP_KEY_XML` to pin a known key (recommended for CI, so rebuilds stay interchangeable). Without it the seeder generates one into the bundle, which still works — the key just changes each build.

### Consuming the bundle

**Self-host / ephemeral environments** — unpack over the app's core volume:

```bash
tar -xzf seeded-core-*.tar.gz -C /etc/bitwarden
```

**Local development, local disk** — unpack anywhere and point the app at it:

```
globalSettings__attachment__baseDirectory=<dir>/core/attachments
globalSettings__dataProtection__directory=<dir>/core/aspnet-dataprotection
```

**Local development, azurite** — attachment paths in the tarball are byte-identical to Azure blob names, so import the tree as-is:

```bash
az storage blob upload-batch \
  --connection-string "UseDevelopmentStorage=true" \
  -d attachments -s core/attachments
```

Leave `attachment.connectionString` set (azurite wins over `baseDirectory`) and point `dataProtection.directory` at the unpacked keys.

> The bundled key is the **filesystem** form. Deployments using `PersistKeysToAzureBlobStorage` expect a single aggregated `keys.xml` in an `aspnet-dataprotection` container instead, so the key needs converting for that path.

## Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `PUSH` | `false` | Set to `true` to push images to ACR |
| `REGISTRY` | `bitwardenprod.azurecr.io` | ACR registry |
| `GIT_SHA` | Current HEAD | Git SHA for versioned tag |
| `DP_KEY_XML` | (empty) | Data protection key XML content (for CI) |
| `KEEP_BUILD_DIR` | (unset) | Set to `1` to preserve the per-preset build directory |
| `ASPNETCORE_ENVIRONMENT` | `Development` | Seeder config environment |

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

**Note**: Both charts also need the [core bundle](#core-bundle) unpacked at `/etc/bitwarden/core` — without the data protection key, login fails against seeded data.
