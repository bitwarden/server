#!/usr/bin/env bash
# Builds a seeded database Docker image for a given preset and database type.
#
# Usage:
#   ./build-seeded-image.sh <preset-name> [db-type]
#
#   db-type: postgres (default), mysql, mariadb, mssql, sqlite
#
# Environment variables:
#   PUSH=true          Push images to ACR after build
#   REGISTRY           ACR registry (default: bitwardenprod.azurecr.io)
#   GIT_SHA            Override git SHA (default: current HEAD short SHA)
#   DP_KEY_XML         Data protection key XML content (for CI; written to key stores)
#   KEEP_BUILD_DIR=1   Preserve the per-preset build directory after completion
#
# Parallel invocations:
#   The script is safe to run concurrently for different <preset, db-type>
#   pairs. Per-invocation isolation comes from:
#     - a unique container name (seeder-build-<db-type>-<tag>)
#     - dynamic host-port binding (the DB port is mapped to an ephemeral host
#       port, discovered via `docker inspect`)
#     - a per-preset Docker build context under docker/<db-type>/build/<tag>/
#   Callers should `dotnet build` the migrations projects and the SeederUtility
#   once before fanning out in parallel — concurrent `dotnet run` invocations
#   from the same project directory will race on bin/obj outputs.
#
# Examples:
#   ./build-seeded-image.sh qa.dunder-mifflin-enterprise-full
#   ./build-seeded-image.sh qa.dunder-mifflin-enterprise-full mysql
#   PUSH=true ./build-seeded-image.sh scale.md-balanced-sterling-cooper mssql
#
#   # Loop over every preset from `preset --list --output json` and build for postgres:
#   dotnet run --project .. -- preset --list --output json \
#     | jq -r '.organization[], .individual[]' \
#     | while read -r preset; do ./build-seeded-image.sh "$preset"; done

set -euo pipefail

PRESET_NAME="${1:?Usage: $0 <preset-name> [db-type]}"
DB_TYPE="${2:-${DB_TYPE:-postgres}}"
REGISTRY="${REGISTRY:-bitwardenprod.azurecr.io}"
GIT_SHA="${GIT_SHA:-$(git rev-parse --short HEAD 2>/dev/null || echo 'unknown')}"
BUILD_DATE="$(date -u +"%Y-%m-%dT%H:%M:%SZ")"
PUSH="${PUSH:-false}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SEEDER_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"
REPO_ROOT="$(cd "${SEEDER_DIR}/../.." && pwd)"
DOCKER_DIR="${SEEDER_DIR}/docker/${DB_TYPE}"
METADATA_FILE="${SCRIPT_DIR}/presets-metadata.json"

# --- Validate DB type ---
case "${DB_TYPE}" in
  postgres|mysql|mariadb|mssql|sqlite) ;;
  *)
    echo "ERROR: Unknown database type '${DB_TYPE}'. Supported: postgres, mysql, mariadb, mssql, sqlite"
    exit 1
    ;;
esac

# Sanitize preset name for Docker tag + container name: replace dots with dashes
TAG="${PRESET_NAME//./-}"
IMAGE_REPO="${REGISTRY}/shot/seeded-${DB_TYPE}"
IMAGE_STABLE="${IMAGE_REPO}:${TAG}"
IMAGE_VERSIONED="${IMAGE_REPO}:${TAG}-${GIT_SHA}"

CONTAINER_NAME="seeder-build-${DB_TYPE}-${TAG}"
WORK_DIR="${DOCKER_DIR}/build/${TAG}"

# --- Cleanup on any exit (partial failures shouldn't leave containers behind) ---
cleanup() {
    local status=$?
    docker rm -f "${CONTAINER_NAME}" >/dev/null 2>&1 || true
    if [[ "${KEEP_BUILD_DIR:-0}" != "1" && -d "${WORK_DIR}" ]]; then
        rm -rf "${WORK_DIR}"
    fi
    return "${status}"
}
trap cleanup EXIT

# Look up preset metadata for OCI labels. Requires `jq`. Missing entries
# fall back to sensible defaults so the build still succeeds; downstream
# tooling that reads labels should handle empty values gracefully.
PRESET_CATEGORY="unknown"
PRESET_DESCRIPTION=""
if command -v jq >/dev/null 2>&1 && [[ -f "${METADATA_FILE}" ]]; then
    PRESET_CATEGORY=$(jq -r --arg p "${PRESET_NAME}" \
        '.presets[$p].category // "unknown"' "${METADATA_FILE}")
    PRESET_DESCRIPTION=$(jq -r --arg p "${PRESET_NAME}" \
        '.presets[$p].description // ""' "${METADATA_FILE}")
else
    echo "WARNING: jq or ${METADATA_FILE} not found; OCI labels will be minimal"
fi

echo "==> Building seeded ${DB_TYPE} image for preset: ${PRESET_NAME}"
echo "    Stable:    ${IMAGE_STABLE}"
echo "    Versioned: ${IMAGE_VERSIONED}"
echo "    Git SHA:   ${GIT_SHA}"
echo "    Category:  ${PRESET_CATEGORY}"
echo "    Desc:      ${PRESET_DESCRIPTION}"
echo "    Container: ${CONTAINER_NAME}"
echo "    Build dir: ${WORK_DIR}"

# --- Prepare per-preset build context ---
rm -rf "${WORK_DIR}"
mkdir -p "${WORK_DIR}"
cp "${DOCKER_DIR}/Dockerfile" "${WORK_DIR}/Dockerfile"
if [[ "${DB_TYPE}" == "mssql" ]]; then
    cp "${DOCKER_DIR}/docker-entrypoint.sh" "${WORK_DIR}/docker-entrypoint.sh"
fi

# ============================================================
# Docker build and push (shared for all DB types)
# ============================================================
_docker_build_and_push() {
    echo "==> Building Docker image"
    docker buildx build \
        --platform linux/amd64 \
        --build-arg "PRESET_NAME=${PRESET_NAME}" \
        --build-arg "PRESET_CATEGORY=${PRESET_CATEGORY}" \
        --build-arg "PRESET_DESCRIPTION=${PRESET_DESCRIPTION}" \
        --build-arg "DB_TYPE=${DB_TYPE}" \
        --build-arg "GIT_SHA=${GIT_SHA}" \
        --build-arg "BUILD_DATE=${BUILD_DATE}" \
        -t "${IMAGE_STABLE}" \
        -t "${IMAGE_VERSIONED}" \
        "${WORK_DIR}" \
        --load

    echo "==> Built: ${IMAGE_STABLE}"
    echo "==> Built: ${IMAGE_VERSIONED}"

    if [[ "${PUSH}" == "true" ]]; then
        # Caller is responsible for registry auth (e.g. `az acr login` in CI or
        # locally) before invoking with PUSH=true.
        echo "==> Pushing images"
        docker push "${IMAGE_STABLE}"
        docker push "${IMAGE_VERSIONED}"
        echo "==> Pushed: ${IMAGE_STABLE}"
        echo "==> Pushed: ${IMAGE_VERSIONED}"

        # free up disk after push
        docker rmi "${IMAGE_STABLE}" "${IMAGE_VERSIONED}" >/dev/null 2>&1 || true
    fi
}

# --- DB-type configuration ---
# INTERNAL_PORT: the port the database listens on inside the container.
# HOST_PORT is discovered post-start via `docker inspect`.
case "${DB_TYPE}" in
  postgres)
    INTERNAL_PORT=5432
    DB_NAME="vault_dev"
    DB_USER="postgres"
    DB_PASS="Password1!"
    MIGRATIONS_DIR="${REPO_ROOT}/util/PostgresMigrations"
    ;;
  mysql)
    INTERNAL_PORT=3306
    DB_NAME="vault_dev"
    DB_USER="root"
    DB_PASS="Password1!"
    MIGRATIONS_DIR="${REPO_ROOT}/util/MySqlMigrations"
    ;;
  mariadb)
    INTERNAL_PORT=3306
    DB_NAME="vault_dev"
    DB_USER="root"
    DB_PASS="Password1!"
    MIGRATIONS_DIR="${REPO_ROOT}/util/MySqlMigrations"
    ;;
  mssql)
    INTERNAL_PORT=1433
    DB_NAME="vault_dev"
    DB_USER="SA"
    # MSSQL requires a complex password (uppercase, number, symbol)
    DB_PASS="Password1!Strong"
    MIGRATIONS_DIR="${REPO_ROOT}/util/MsSqlMigratorUtility"
    ;;
  sqlite)
    DB_NAME="vault_dev"
    SQLITE_FILE="${WORK_DIR}/seed.db"
    MIGRATIONS_DIR="${REPO_ROOT}/util/SqliteMigrations"
    ;;
esac

# --- Data protection key setup ---
DP_KEYS_DIR="${HOME}/.aspnet/DataProtection-Keys"
DP_KEY_FILENAME="key-9aa06f19-9afe-414b-8791-189be3b5650f.xml"
DP_KEY_SRC="${SEEDER_DIR}/docker/dp-keys/${DP_KEY_FILENAME}"
mkdir -p "${DP_KEYS_DIR}" "${SEEDER_DIR}/docker/dp-keys"

if [[ -n "${DP_KEY_XML:-}" ]]; then
    echo "==> Writing data protection key from environment variable"
    echo "${DP_KEY_XML}" > "${DP_KEYS_DIR}/${DP_KEY_FILENAME}"
    echo "${DP_KEY_XML}" > "${DP_KEY_SRC}"
elif [[ -f "${DP_KEY_SRC}" ]]; then
    echo "==> Copying data protection key to system key store"
    cp "${DP_KEY_SRC}" "${DP_KEYS_DIR}/"
else
    echo "WARNING: Data protection key not found at ${DP_KEY_SRC}."
    echo "         Encrypted fields may not be decryptable in the target environment."
fi

# ============================================================
# SQLite — no container needed, seeder writes directly to file
# ============================================================
if [[ "${DB_TYPE}" == "sqlite" ]]; then
    echo "==> Running SQLite migrations"
    cd "${MIGRATIONS_DIR}"
    dotnet ef database update \
        --connection "Data Source=${SQLITE_FILE}"

    echo "==> Seeding SQLite database with preset: ${PRESET_NAME}"
    cd "${SEEDER_DIR}"
    globalSettings__databaseProvider=sqlite \
    globalSettings__sqlite__connectionString="Data Source=${SQLITE_FILE}" \
    dotnet run --project . -- preset --name "${PRESET_NAME}"

    # Metadata injected directly into the SQLite file via sqlite3 if available
    if command -v sqlite3 &>/dev/null; then
        sqlite3 "${SQLITE_FILE}" \
            "CREATE TABLE IF NOT EXISTS \"_SeederMetadata\" (\"Key\" TEXT PRIMARY KEY, \"Value\" TEXT);
             INSERT OR REPLACE INTO \"_SeederMetadata\" VALUES ('preset', '${PRESET_NAME}');
             INSERT OR REPLACE INTO \"_SeederMetadata\" VALUES ('git_sha', '${GIT_SHA}');
             INSERT OR REPLACE INTO \"_SeederMetadata\" VALUES ('built_at', '${BUILD_DATE}');"
    fi

    _docker_build_and_push
    echo "==> Done: ${PRESET_NAME} (${DB_TYPE}) → ${TAG}"
    exit 0
fi

# ============================================================
# Container-based databases
# ============================================================

# --- Start container with a dynamic host port so multiple invocations don't clash ---
echo "==> Starting ${DB_TYPE} container: ${CONTAINER_NAME}"
docker rm -f "${CONTAINER_NAME}" 2>/dev/null || true

case "${DB_TYPE}" in
  postgres)
    docker run -d \
        --name "${CONTAINER_NAME}" \
        -e "POSTGRES_DB=${DB_NAME}" \
        -e "POSTGRES_USER=${DB_USER}" \
        -e "POSTGRES_PASSWORD=${DB_PASS}" \
        -p "0:${INTERNAL_PORT}" \
        postgres:14 >/dev/null
    ;;
  mysql)
    docker run -d \
        --name "${CONTAINER_NAME}" \
        -e "MYSQL_DATABASE=${DB_NAME}" \
        -e "MYSQL_ROOT_PASSWORD=${DB_PASS}" \
        -p "0:${INTERNAL_PORT}" \
        mysql:8.0 \
        --default-authentication-plugin=mysql_native_password >/dev/null
    ;;
  mariadb)
    docker run -d \
        --name "${CONTAINER_NAME}" \
        -e "MARIADB_DATABASE=${DB_NAME}" \
        -e "MARIADB_ROOT_PASSWORD=${DB_PASS}" \
        -p "0:${INTERNAL_PORT}" \
        mariadb:12 >/dev/null
    ;;
  mssql)
    docker run -d \
        --name "${CONTAINER_NAME}" \
        -e "ACCEPT_EULA=Y" \
        -e "MSSQL_PID=Developer" \
        -e "SA_PASSWORD=${DB_PASS}" \
        -p "0:${INTERNAL_PORT}" \
        --platform linux/amd64 \
        mcr.microsoft.com/mssql/server:2022-CU22-ubuntu-22.04 >/dev/null
    ;;
esac

# Discover the ephemeral host port chosen by Docker
HOST_PORT=$(docker inspect \
    --format="{{(index (index .NetworkSettings.Ports \"${INTERNAL_PORT}/tcp\") 0).HostPort}}" \
    "${CONTAINER_NAME}")
echo "==> ${DB_TYPE} host port: ${HOST_PORT}"

# --- Wait for readiness (bounded so a stuck container fails fast) ---
READY_TIMEOUT_SECS=300
wait_until_ready() {
    local deadline=$(( $(date +%s) + READY_TIMEOUT_SECS ))
    while ! "$@" &>/dev/null; do
        if (( $(date +%s) >= deadline )); then
            echo "ERROR: ${DB_TYPE} did not become ready within ${READY_TIMEOUT_SECS}s"
            docker logs --tail 50 "${CONTAINER_NAME}" || true
            return 1
        fi
        sleep 2
    done
}

echo "==> Waiting for ${DB_TYPE} to be ready (timeout ${READY_TIMEOUT_SECS}s)..."
case "${DB_TYPE}" in
  postgres)
    wait_until_ready docker exec "${CONTAINER_NAME}" \
        pg_isready -U "${DB_USER}" -d "${DB_NAME}"
    ;;
  mysql|mariadb)
    wait_until_ready docker exec "${CONTAINER_NAME}" \
        sh -c 'mysqladmin ping -u root -p"'"${DB_PASS}"'" --silent 2>/dev/null || mariadb-admin ping -u root -p"'"${DB_PASS}"'" --silent 2>/dev/null'
    ;;
  mssql)
    wait_until_ready docker exec "${CONTAINER_NAME}" \
        /opt/mssql-tools18/bin/sqlcmd \
            -S localhost -U SA -P "${DB_PASS}" -C \
            -Q "SELECT 1"
    ;;
esac
echo "==> ${DB_TYPE} ready"

# --- Run migrations ---
echo "==> Running database migrations"
case "${DB_TYPE}" in
  postgres)
    cd "${MIGRATIONS_DIR}"
    dotnet ef database update \
        -- --globalSettings:postgreSql:connectionString="Host=localhost;Port=${HOST_PORT};Database=${DB_NAME};Username=${DB_USER};Password=${DB_PASS}"
    ;;
  mysql|mariadb)
    cd "${MIGRATIONS_DIR}"
    dotnet ef database update \
        -- --globalSettings:databaseProvider=mysql \
           --globalSettings:mySql:connectionString="Server=localhost;Port=${HOST_PORT};Database=${DB_NAME};Uid=${DB_USER};Pwd=${DB_PASS};"
    ;;
  mssql)
    cd "${MIGRATIONS_DIR}"
    dotnet run -- \
        "Server=localhost,${HOST_PORT};Database=${DB_NAME};User Id=${DB_USER};Password=${DB_PASS};TrustServerCertificate=true;"
    ;;
esac

# --- Seed ---
echo "==> Seeding database with preset: ${PRESET_NAME}"
cd "${SEEDER_DIR}"
case "${DB_TYPE}" in
  postgres)
    globalSettings__databaseProvider=postgreSql \
    globalSettings__postgreSql__connectionString="Host=localhost;Port=${HOST_PORT};Database=${DB_NAME};Username=${DB_USER};Password=${DB_PASS}" \
    dotnet run --project . -- preset --name "${PRESET_NAME}"
    ;;
  mysql|mariadb)
    globalSettings__databaseProvider=mySQL \
    globalSettings__mySql__connectionString="Server=localhost;Port=${HOST_PORT};Database=${DB_NAME};Uid=${DB_USER};Pwd=${DB_PASS};" \
    dotnet run --project . -- preset --name "${PRESET_NAME}"
    ;;
  mssql)
    globalSettings__databaseProvider=sqlServer \
    globalSettings__sqlServer__connectionString="Server=localhost,${HOST_PORT};Database=${DB_NAME};User Id=${DB_USER};Password=${DB_PASS};TrustServerCertificate=true;" \
    dotnet run --project . -- preset --name "${PRESET_NAME}"
    ;;
esac

# --- Write metadata, dump, cleanup ---
case "${DB_TYPE}" in
  postgres)
    docker exec "${CONTAINER_NAME}" \
        pg_dump --no-owner --no-acl -U "${DB_USER}" -d "${DB_NAME}" > "${WORK_DIR}/seed.sql"

    cat >> "${WORK_DIR}/seed.sql" << EOF

-- Seeder metadata
CREATE TABLE IF NOT EXISTS "_SeederMetadata" ("Key" text PRIMARY KEY, "Value" text);
INSERT INTO "_SeederMetadata" VALUES ('preset', '${PRESET_NAME}') ON CONFLICT ("Key") DO UPDATE SET "Value" = EXCLUDED."Value";
INSERT INTO "_SeederMetadata" VALUES ('git_sha', '${GIT_SHA}') ON CONFLICT ("Key") DO UPDATE SET "Value" = EXCLUDED."Value";
INSERT INTO "_SeederMetadata" VALUES ('built_at', '${BUILD_DATE}') ON CONFLICT ("Key") DO UPDATE SET "Value" = EXCLUDED."Value";
EOF
    ;;

  mysql|mariadb)
    docker exec "${CONTAINER_NAME}" \
        sh -c 'mysqldump -u root -p"'"${DB_PASS}"'" --no-tablespaces "'"${DB_NAME}"'" 2>/dev/null || mariadb-dump -u root -p"'"${DB_PASS}"'" --no-tablespaces "'"${DB_NAME}"'" 2>/dev/null' > "${WORK_DIR}/seed.sql"

    cat >> "${WORK_DIR}/seed.sql" << EOF

-- Seeder metadata
CREATE TABLE IF NOT EXISTS \`_SeederMetadata\` (\`Key\` varchar(255) PRIMARY KEY, \`Value\` text);
INSERT INTO \`_SeederMetadata\` VALUES ('preset', '${PRESET_NAME}') ON DUPLICATE KEY UPDATE \`Value\` = VALUES(\`Value\`);
INSERT INTO \`_SeederMetadata\` VALUES ('git_sha', '${GIT_SHA}') ON DUPLICATE KEY UPDATE \`Value\` = VALUES(\`Value\`);
INSERT INTO \`_SeederMetadata\` VALUES ('built_at', '${BUILD_DATE}') ON DUPLICATE KEY UPDATE \`Value\` = VALUES(\`Value\`);
EOF
    ;;

  mssql)
    # Write metadata before backup so it's baked in
    docker exec "${CONTAINER_NAME}" \
        /opt/mssql-tools18/bin/sqlcmd \
            -S localhost -U SA -P "${DB_PASS}" -C \
            -d "${DB_NAME}" \
            -Q "IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='_SeederMetadata' AND xtype='U') CREATE TABLE [_SeederMetadata] ([Key] nvarchar(255) PRIMARY KEY, [Value] nvarchar(max)); MERGE [_SeederMetadata] AS t USING (VALUES ('preset','${PRESET_NAME}'),('git_sha','${GIT_SHA}'),('built_at','${BUILD_DATE}')) AS s([Key],[Value]) ON t.[Key]=s.[Key] WHEN MATCHED THEN UPDATE SET [Value]=s.[Value] WHEN NOT MATCHED THEN INSERT VALUES(s.[Key],s.[Value]);"

    # Copy MDF/LDF files directly — avoids RESTORE issues on Kubernetes PVCs
    docker exec "${CONTAINER_NAME}" \
        /opt/mssql-tools18/bin/sqlcmd \
            -S localhost -U SA -P "${DB_PASS}" -C \
            -Q "ALTER DATABASE [${DB_NAME}] SET OFFLINE WITH ROLLBACK IMMEDIATE"
    docker cp "${CONTAINER_NAME}:/var/opt/mssql/data/${DB_NAME}.mdf" "${WORK_DIR}/${DB_NAME}.mdf"
    docker cp "${CONTAINER_NAME}:/var/opt/mssql/data/${DB_NAME}_log.ldf" "${WORK_DIR}/${DB_NAME}_log.ldf"
    ;;
esac

echo "==> Stopping ${DB_TYPE} container"
docker rm -f "${CONTAINER_NAME}" >/dev/null

_docker_build_and_push
echo "==> Done: ${PRESET_NAME} (${DB_TYPE}) → ${TAG}"
