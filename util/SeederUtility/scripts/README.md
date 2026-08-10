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

Either tag works with any core bundle, because CI pins one data protection key for every build.

Local builds tag for `bitwardenprod.azurecr.io/shot/` and push there only when you pass `PUSH=true`. The GitHub Actions workflow sets `PUSH: "false"` and has no registry login, so nothing it builds reaches the registry. Take those images from the run's artifacts instead.

## Getting an image from a CI build

Each matrix job uploads two artifacts, named after the database and preset. They are deleted 7 days after the run.

```bash
RUN=31203415095
PRESET=qa.dunder-mifflin-enterprise-full

gh run download "$RUN" --name "seeded-postgres-$PRESET"
docker load -i seeded-postgres-*.tar

gh run download "$RUN" --name "seeded-core-postgres-$PRESET"
tar -xf seeded-core-postgres-*.tar.gz -C ~/bitwarden-seed
```

Use `tar -xf` rather than double-clicking the tarball. Browsers may decompress it during download while keeping the `.tar.gz` name, and macOS Archive Utility then reports "unsupported format". Run `file` on it to tell the two apart.

Start the database and point the application at the unpacked bundle:

```bash
docker run -d -p 5432:5432 \
  bitwardenprod.azurecr.io/shot/seeded-postgres:qa-dunder-mifflin-enterprise-full
```

The seed runs on first boot for postgres, mysql, and mariadb, so the server accepts connections before the data is loaded. Poll for a seeded table rather than trusting `pg_isready`:

```bash
until docker exec <container> psql -U postgres -d vault_dev \
  -tAc 'select 1 from "Organization" limit 1' >/dev/null 2>&1; do sleep 2; done
```

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

The application reads two of the seeder's outputs, not the database, so the database image cannot carry them. Data protection keys come first: the seeder encrypts `MasterPassword`, `Key`, and `PrivateKey` with ASP.NET Data Protection, and logins fail without the same key. Attachment blobs are the other, since the database holds only attachment metadata.

In a deployment both live under `/etc/bitwarden/core`, so each build writes them to one tarball next to the image:

```
docker/bundles/seeded-core-{db}-{preset}-{git-sha}.tar.gz
└── core/
    ├── aspnet-dataprotection/key-….xml
    └── attachments/{cipherId}/{attachmentId}
```

CI pulls the key from the `gh-org-bitwarden` Azure Key Vault as `DP-KEY-XML` and passes it in as `DP_KEY_XML`, so every build and every database in a build share one key and their bundles are interchangeable. A local build without `DP_KEY_XML` falls back to `docker/dp-keys/`. With neither, the build fails rather than letting Data Protection mint a throwaway key, which would produce an image whose encrypted fields open only with that one build's bundle.

### Consuming the bundle

The bundle's `core/` layout matches classic self-host, where the app reads `/etc/bitwarden/core`. Unpack it over that volume:

```bash
tar -xzf seeded-core-*.tar.gz -C /etc/bitwarden
```

BW Lite reads different paths, so the same command puts the key somewhere lite never reads and login fails. See [Running BW Lite against a seeded image](#running-bw-lite-against-a-seeded-image) for the layout it expects.

For local development on local disk, unpack anywhere and point the app at it:

```
globalSettings__attachment__baseDirectory=<dir>/core/attachments
globalSettings__dataProtection__directory=<dir>/core/aspnet-dataprotection
```

For local development on azurite, the attachment paths in the tarball match Azure blob names exactly, so import the tree as-is:

```bash
az storage blob upload-batch \
  --connection-string "UseDevelopmentStorage=true" \
  -d attachments -s core/attachments
```

Leave `attachment.connectionString` set (azurite wins over `baseDirectory`) and point `dataProtection.directory` at the unpacked keys.

> The bundled key is the **filesystem** form. Deployments using `PersistKeysToAzureBlobStorage` expect a single aggregated `keys.xml` in an `aspnet-dataprotection` container instead, so the key needs converting for that path.

## Running BW Lite against a seeded image

Load the image and unpack the bundle first, as described in [Getting an image from a CI build](#getting-an-image-from-a-ci-build).

Lite reads `/etc/bitwarden/data-protection`, `/etc/bitwarden/attachments`, and `/etc/bitwarden/licenses`, which do not match the bundle's `core/` layout. Stage a directory in the shape lite expects:

```bash
mkdir -p ~/bwlite-etc/{data-protection,attachments,licenses/organization,licenses/user}
cp ~/bitwarden-seed/core/aspnet-dataprotection/*.xml ~/bwlite-etc/data-protection/
cp -R ~/bitwarden-seed/core/attachments/. ~/bwlite-etc/attachments/
```

Start the database on a named network:

```bash
docker network create bwlite
docker run -d --name bwlite-db --network bwlite -p 5433:5432 bitwardenprod.azurecr.io/shot/seeded-postgres:scale-lg-balanced-wayne-enterprises
```

Start lite against it:

```bash
docker run -d --name bwlite --network bwlite -p 8080:8080 -v "$HOME/bwlite-etc:/etc/bitwarden" -e BW_DOMAIN=localhost:8080 -e BW_DB_PROVIDER=postgresql -e BW_DB_SERVER=bwlite-db -e BW_DB_PORT=5432 -e BW_DB_DATABASE=vault_dev -e BW_DB_USERNAME=postgres -e BW_DB_PASSWORD='Password1!' -e BW_INSTALLATION_ID=e6b8a9c4-0d3f-4a71-9c2e-5f7a1b3d8e02 -e BW_INSTALLATION_KEY=seederlocaltest ghcr.io/bitwarden/lite:beta
```

Confirm all six services start:

```bash
docker logs bwlite 2>&1 | grep -E "entered RUNNING state|FATAL state"
```

Open `http://localhost:8080` and log in as the preset's owner. Seeded accounts use the password `asdfasdfasdf` unless the preset overrides it.

Notes:

- Pass `BW_INSTALLATION_ID`, not `globalSettings__installation__id`. The entrypoint overwrites the latter with an empty string, and every service then dies on a Guid parse error. The symptom is a supervisord loop of `terminated by SIGABRT` and `entered FATAL state`, with only nginx surviving.
- A successful login confirms the data protection key is correct. The seeder encrypts `MasterPassword`, `Key`, and `PrivateKey`, so nothing authenticates without it.
- Admins see only the collections assigned to them, because presets leave `AllowAdminAccessToAllCollectionItems` off. The owner of a large org sees a small slice of it.
- Seeded organizations have no license file, so `ValidateOrganizationsAsync` disables them within twelve hours on self-host. Short sessions are unaffected.

## Running self-host against a seeded image

Load the image and unpack the bundle as described in [Getting an image from a CI build](#getting-an-image-from-a-ci-build). Self-host reads `/etc/bitwarden/core`, which the bundle's `core/` layout already matches, so it unpacks straight into `bwdata`.

Get an installation id from https://bitwarden.com/host. The Setup container validates it against the Bitwarden API, so an invented one fails.

```bash
./bitwarden.sh install ~/bwdata
```

Swap the database in `bwdata/docker/docker-compose.override.yml`, which `run.sh` merges automatically:

```yaml
services:
  mssql:
    image: bitwardenprod.azurecr.io/shot/seeded-mssql:qa-dunder-mifflin-enterprise-full
    pull_policy: never
```

`pull_policy: never` applies to images loaded from a CI artifact. `run.sh` runs `docker compose pull` on start, and the tag names a registry the artifact was never pushed to, so the pull fails without it. Drop it once you are pulling an image that really is in the registry, or compose will keep using a stale local copy.

Unpack the bundle, then start:

```bash
tar -xzf seeded-core-mssql-*.tar.gz -C ~/bwdata
./bitwarden.sh start ~/bwdata
```

Log in at the URL the installer prints, using the preset's owner account.

### Match the app version to the image

An image carries the schema from the commit it was built at, including stored procedures. `bitwarden.sh` pins a released core version, so an image built from `main` can be missing procedures that release still calls, and login fails with `Could not find stored procedure`. Build from the release branch the deployment runs, or pin the app images to a tag built from the same commit.

## Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `PUSH` | `false` | Set to `true` to push images to ACR |
| `REGISTRY` | `bitwardenprod.azurecr.io` | ACR registry |
| `GIT_SHA` | Current HEAD | Git SHA for versioned tag |
| `DP_KEY_XML` | (empty) | Data protection key XML content. CI supplies this from Key Vault; locally it falls back to `docker/dp-keys/` |
| `KEEP_BUILD_DIR` | (unset) | Set to `1` to preserve the per-preset build directory |

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

**Note**: Both charts also need the [core bundle](#core-bundle) unpacked at `/etc/bitwarden/core`. Login fails against seeded data without the data protection key.
