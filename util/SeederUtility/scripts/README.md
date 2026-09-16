# Seeded Database Build Pipeline

Builds pre-seeded database Docker images from seeder presets, so a deployment can start from seeded data without running the seeder itself.

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
| `mssql` | `mcr.microsoft.com/mssql/server:2025-CU5-ubuntu-24.04` | MDF/LDF file copy → `CREATE DATABASE ... FOR ATTACH` |

`mysql` and `mariadb` share the same migrations project and seeded data; they differ only in the engine the dump is produced from and restored into.

### MSSQL Notes

- MSSQL uses file attach (`CREATE DATABASE ... FOR ATTACH`) instead of `.bak` restore. The `.bak` restore approach fails on Kubernetes PVCs due to `ValidateTargetForCreation` errors — a known issue with MSSQL on certain storage backends.
- The entrypoint waits for all system databases to be ONLINE and verifies the data directory is writable (by creating and dropping a test database) before attempting the attach.
- The database is restored as `vault_dev`, the seeder's default name. Self-host expects `vault` by default, so override the database name in its connection string when consuming this image.

## Image Tags

Each build produces two tags:

- **Latest**: `seeded-{db}:{preset-name}-latest` — e.g. `seeded-postgres:qa-dunder-mifflin-enterprise-full-latest`. Moves with every build.
- **Versioned**: `seeded-{db}:{preset-name}-{git-sha}` — e.g. `seeded-postgres:qa-dunder-mifflin-enterprise-full-abc1234`. Immutable, so a deployment can pin a known build.

Either tag works with any copy of the data protection key, because CI pins one key for every build.

Local builds tag for `devimagesaedgdev.azurecr.io` and push there only when you pass `PUSH=true`. The GitHub Actions workflow pushes every image it builds.

## Getting an image from a CI build

Each matrix job pushes both tags to `devimagesaedgdev`. The registry refuses anonymous pulls, so log in first.

```bash
az acr login -n devimagesaedgdev
docker pull devimagesaedgdev.azurecr.io/seeded-postgres:qa-dunder-mifflin-enterprise-full-latest
```

### Getting the data protection key

CI does not publish the key, so fetch it from Key Vault yourself. Every build shares the same key, so you only do this once per environment.

`DP-KEY-XML` exists to encrypt seeded fixtures and nothing else. Do not reuse it in an environment that holds real vault data.

```bash
mkdir -p ~/bitwarden-seed/core/aspnet-dataprotection
az keyvault secret show --vault-name gh-org-bitwarden --name DP-KEY-XML --query value -o tsv \
  > ~/bitwarden-seed/core/aspnet-dataprotection/key-9aa06f19-9afe-414b-8791-189be3b5650f.xml
```

Attachment blobs go to a separate artifact, `seeded-attachments-{db}-{preset}`, holding `{cipherId}/{attachmentId}` at its root. Presets without attachments upload nothing, so the artifact is absent.

```bash
RUN=$(gh run list --workflow build-seeded-databases.yml --status success \
  -L 1 --json databaseId --jq '.[0].databaseId')
PRESET=qa.dunder-mifflin-enterprise-full

gh run download "$RUN" --name "seeded-attachments-postgres-$PRESET" \
  -D ~/bitwarden-seed/core/attachments
```

Start the database:

```bash
docker run -d -p 5432:5432 \
  devimagesaedgdev.azurecr.io/seeded-postgres:qa-dunder-mifflin-enterprise-full-latest
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
org.opencontainers.image.revision=abc1234
org.opencontainers.image.created=2026-04-16T00:00:00Z
```

The preset name is `<category>.<name>`, where the category matches the fixture folder under `Seeds/fixtures/presets/`.

## Core Bundle

The application reads two of the seeder's outputs, not the database, so the database image cannot carry them. Data protection keys come first: the seeder encrypts `MasterPassword`, `Key`, and `PrivateKey` with ASP.NET Data Protection, and logins fail without the same key. Attachment blobs are the other, since the database holds only attachment metadata.

In a deployment both live under `/etc/bitwarden/core`, so each build writes them to one tarball next to the image, which CI does not publish:

```
docker/bundles/seeded-core-{db}-{preset}-{git-sha}.tar.gz
└── core/
    ├── aspnet-dataprotection/key-….xml
    └── attachments/{cipherId}/{attachmentId}
```

CI pulls the key from the `gh-org-bitwarden` Azure Key Vault as `DP-KEY-XML` and passes it in as `DP_KEY_XML`, so every build and every database in a build share one key and their bundles are interchangeable. A local build without `DP_KEY_XML` falls back to `docker/dp-keys/`. With neither, the build fails rather than letting Data Protection mint a throwaway key, which would produce an image whose encrypted fields open only with that one build's bundle.

### Consuming the bundle

A local build leaves the tarball in `docker/bundles/`. Its `core/` layout matches classic self-host, where the app reads `/etc/bitwarden/core`, so unpack it over that volume:

```bash
tar -xzf seeded-core-*.tar.gz -C /etc/bitwarden
```

Coming from a CI build there is no tarball to unpack — assemble the same `core/` layout by hand, as described in [Getting the data protection key](#getting-the-data-protection-key).

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

Load the image and fetch the key first, as described in [Getting an image from a CI build](#getting-an-image-from-a-ci-build).

Lite reads `/etc/bitwarden/data-protection`, `/etc/bitwarden/attachments`, and `/etc/bitwarden/licenses`, which do not match the bundle's `core/` layout. Stage a directory in the shape lite expects:

```bash
mkdir -p ~/bwlite-etc/{data-protection,attachments,licenses/organization,licenses/user}
cp ~/bitwarden-seed/core/aspnet-dataprotection/*.xml ~/bwlite-etc/data-protection/
```

For attachment presets, also copy `core/attachments/` into `~/bwlite-etc/attachments/`.

Start the database on a named network:

```bash
docker network create bwlite
docker run -d --name bwlite-db --network bwlite -p 5433:5432 devimagesaedgdev.azurecr.io/seeded-postgres:scale-lg-balanced-wayne-enterprises-latest
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

Load the image and fetch the key as described in [Getting an image from a CI build](#getting-an-image-from-a-ci-build). Self-host reads `/etc/bitwarden/core`, which matches the `core/` layout staged there, so it copies straight into `bwdata`.

Get an installation id from https://bitwarden.com/host. The Setup container validates it against the Bitwarden API, so an invented one fails.

```bash
./bitwarden.sh install ~/bwdata
```

Swap the database in `bwdata/docker/docker-compose.override.yml`, which `run.sh` merges automatically:

```yaml
services:
  mssql:
    image: devimagesaedgdev.azurecr.io/seeded-mssql:qa-dunder-mifflin-enterprise-full-latest
```

To pull that image, run `az acr login -n devimagesaedgdev` first. The registry refuses anonymous pulls, and `run.sh` runs `docker compose pull` on every start.

A locally built image needs `pull_policy: never`, because without `PUSH=true` the tag names a registry it was never pushed to and the pull fails.

The image restores its database as `vault_dev`, not the `vault` self-host expects by default — override the database name in self-host's connection string before starting.

Gate `admin` on the database. It migrates at startup, and on a fresh volume it will create an empty `vault_dev` before the seed finishes attaching, leaving a schema with no data. The image reports healthy only once the seed is attached:

```yaml
services:
  mssql:
    image: devimagesaedgdev.azurecr.io/seeded-mssql:qa-dunder-mifflin-enterprise-full-abc1234
    pull_policy: never

  admin:
    depends_on:
      mssql:
        condition: service_healthy
```

Copy the key into place, then start:

```bash
mkdir -p ~/bwdata/core/aspnet-dataprotection
cp ~/bitwarden-seed/core/aspnet-dataprotection/*.xml ~/bwdata/core/aspnet-dataprotection/
./bitwarden.sh start ~/bwdata
```

For attachment presets, also copy `core/attachments/` into `~/bwdata/core/attachments/`.

Log in at the URL the installer prints, using the preset's owner account.

### Match the app version to the image

An image older than the deployment is fine. Admin migrates the seeded database forward on startup, keeping the data.

The other direction breaks. `bitwarden.sh` pins a released core version, so an image built from `main` can be missing procedures that release still calls, and no migration can restore a dropped one. Login fails with `Could not find stored procedure`. Build from the release branch the deployment runs, or pin the app images to a tag built from the same commit.

Rule out the cheaper cause first. SQL Server has no arm64 build, so on Apple Silicon it runs emulated and can hit an assertion failure that leaves it reporting existing procedures as missing. Run `docker restart bitwarden-mssql` and try again before chasing a version mismatch.

## Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `PUSH` | `false` | Set to `true` to push images to ACR |
| `REGISTRY` | `devimagesaedgdev.azurecr.io` | ACR registry |
| `GIT_SHA` | Current HEAD | Git SHA for versioned tag |
| `DP_KEY_XML` | (empty) | Data protection key XML content. CI supplies this from Key Vault; locally it falls back to `docker/dp-keys/` |
| `KEEP_BUILD_DIR` | (unset) | Set to `1` to preserve the per-preset build directory |

## GitHub Actions

The workflow at `.github/workflows/build-seeded-databases.yml` supports:

- **Manual dispatch**: Build a single preset + database type. Leave `preset` empty for the curated default list, or set it to `all` to build every preset. Leave `database` as `all` to build the full database matrix.
- **Cron**: Every Sunday at 2am UTC, rebuilds the curated default preset list (`_DEFAULT_PRESETS`) × all database types

The workflow uses a matrix strategy (`preset × database`) with `fail-fast: false`.

## Using seeded images with the self-host Helm chart

Point the chart's database image at a seeded tag in [bitwarden/charts](https://github.com/bitwarden/charts):

```yaml
# values.yaml
self-host:
  database:
    image:
      name: devimagesaedgdev.azurecr.io/seeded-mssql
      tag: qa-dunder-mifflin-enterprise-full-latest
```

**Note**: The chart also needs the [data protection key](#getting-the-data-protection-key) at `/etc/bitwarden/core/aspnet-dataprotection`, plus `core/attachments` for attachment presets. Login fails against seeded data without the key.
