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
#
# Examples:
#   ./build-seeded-image.sh qa.dunder-mifflin-enterprise-full
#   ./build-seeded-image.sh qa.dunder-mifflin-enterprise-full mysql
#   PUSH=true ./build-seeded-image.sh scale.md-balanced-sterling-cooper mssql

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

# Sanitize preset name for Docker tag: replace dots with dashes
TAG="${PRESET_NAME//./-}"
IMAGE_REPO="${REGISTRY}/shot/seeded-${DB_TYPE}"
IMAGE_STABLE="${IMAGE_REPO}:${TAG}"
IMAGE_VERSIONED="${IMAGE_REPO}:${TAG}-${GIT_SHA}"

echo "==> Building seeded ${DB_TYPE} image for preset: ${PRESET_NAME}"
echo "    Stable:    ${IMAGE_STABLE}"
echo "    Versioned: ${IMAGE_VERSIONED}"
echo "    Git SHA:   ${GIT_SHA}"

# ============================================================
# Docker build and push (shared for all DB types)
# ============================================================
_docker_build_and_push() {
    echo "==> Building Docker image"
    docker buildx build \
        --platform linux/amd64 \
        --build-arg "PRESET_NAME=${PRESET_NAME}" \
        --build-arg "GIT_SHA=${GIT_SHA}" \
        --build-arg "BUILD_DATE=${BUILD_DATE}" \
        -t "${IMAGE_STABLE}" \
        -t "${IMAGE_VERSIONED}" \
        "${DOCKER_DIR}" \
        --load

    echo "==> Built: ${IMAGE_STABLE}"
    echo "==> Built: ${IMAGE_VERSIONED}"

    if [[ "${PUSH}" == "true" ]]; then
        echo "==> Logging in to ACR"
        az acr login --name bitwardenprod

        echo "==> Pushing images"
        docker push "${IMAGE_STABLE}"
        docker push "${IMAGE_VERSIONED}"
        echo "==> Pushed: ${IMAGE_STABLE}"
        echo "==> Pushed: ${IMAGE_VERSIONED}"
    fi
}

# --- Validate DB type ---
case "${DB_TYPE}" in
  postgres|mysql|mariadb|mssql|sqlite) ;;
  *)
    echo "ERROR: Unknown database type '${DB_TYPE}'. Supported: postgres, mariadb, mssql, sqlite"
    exit 1
    ;;
esac

# --- DB-type configuration ---
case "${DB_TYPE}" in
  postgres)
    CONTAINER_NAME="seeder-build-postgres"
    DB_PORT=5432
    DB_NAME="vault_dev"
    DB_USER="postgres"
    DB_PASS="Password1!"
    MIGRATIONS_DIR="${REPO_ROOT}/util/PostgresMigrations"
    ;;
  mysql)
    CONTAINER_NAME="seeder-build-mysql"
    DB_PORT=3306
    DB_NAME="vault_dev"
    DB_USER="root"
    DB_PASS="Password1!"
    MIGRATIONS_DIR="${REPO_ROOT}/util/MySqlMigrations"
    ;;
  mariadb)
    CONTAINER_NAME="seeder-build-mariadb"
    DB_PORT=4306
    DB_NAME="vault_dev"
    DB_USER="root"
    DB_PASS="Password1!"
    MIGRATIONS_DIR="${REPO_ROOT}/util/MySqlMigrations"
    ;;
  mssql)
    CONTAINER_NAME="seeder-build-mssql"
    DB_PORT=1433
    DB_NAME="vault_dev"
    DB_USER="SA"
    # MSSQL requires a complex password (uppercase, number, symbol)
    DB_PASS="Password1!Strong"
    MIGRATIONS_DIR="${REPO_ROOT}/util/MsSqlMigratorUtility"
    ;;
  sqlite)
    CONTAINER_NAME=""
    DB_NAME="vault_dev"
    SQLITE_FILE="${DOCKER_DIR}/seed.db"
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

mkdir -p "${DOCKER_DIR}"

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
    exit 0
fi

# ============================================================
# Container-based databases
# ============================================================

# --- Start container ---
echo "==> Starting ${DB_TYPE} container: ${CONTAINER_NAME}"
docker rm -f "${CONTAINER_NAME}" 2>/dev/null || true

case "${DB_TYPE}" in
  postgres)
    docker run -d \
        --name "${CONTAINER_NAME}" \
        -e "POSTGRES_DB=${DB_NAME}" \
        -e "POSTGRES_USER=${DB_USER}" \
        -e "POSTGRES_PASSWORD=${DB_PASS}" \
        -p "${DB_PORT}:5432" \
        postgres:14
    ;;
  mysql)
    docker run -d \
        --name "${CONTAINER_NAME}" \
        -e "MYSQL_DATABASE=${DB_NAME}" \
        -e "MYSQL_ROOT_PASSWORD=${DB_PASS}" \
        -p "${DB_PORT}:3306" \
        mysql:8.0 \
        --default-authentication-plugin=mysql_native_password
    ;;
  mariadb)
    docker run -d \
        --name "${CONTAINER_NAME}" \
        -e "MARIADB_DATABASE=${DB_NAME}" \
        -e "MARIADB_ROOT_PASSWORD=${DB_PASS}" \
        -p "${DB_PORT}:3306" \
        mariadb:12
    ;;
  mssql)
    docker run -d \
        --name "${CONTAINER_NAME}" \
        -e "ACCEPT_EULA=Y" \
        -e "MSSQL_PID=Developer" \
        -e "SA_PASSWORD=${DB_PASS}" \
        -p "${DB_PORT}:1433" \
        --platform linux/amd64 \
        mcr.microsoft.com/mssql/server:2022-CU22-ubuntu-22.04
    ;;
esac

# --- Wait for readiness ---
echo "==> Waiting for ${DB_TYPE} to be ready..."
case "${DB_TYPE}" in
  postgres)
    until docker exec "${CONTAINER_NAME}" \
        pg_isready -U "${DB_USER}" -d "${DB_NAME}" &>/dev/null; do sleep 1; done
    ;;
  mysql|mariadb)
    until docker exec "${CONTAINER_NAME}" \
        sh -c 'mysqladmin ping -u root -p"'"${DB_PASS}"'" --silent 2>/dev/null || mariadb-admin ping -u root -p"'"${DB_PASS}"'" --silent 2>/dev/null' &>/dev/null; do sleep 2; done
    ;;
  mssql)
    until docker exec "${CONTAINER_NAME}" \
        /opt/mssql-tools18/bin/sqlcmd \
            -S localhost -U SA -P "${DB_PASS}" -C \
            -Q "SELECT 1" &>/dev/null; do sleep 2; done
    ;;
esac
echo "==> ${DB_TYPE} ready"

# --- Run migrations ---
echo "==> Running database migrations"
case "${DB_TYPE}" in
  postgres)
    cd "${MIGRATIONS_DIR}"
    dotnet ef database update \
        --connection "Host=localhost;Port=${DB_PORT};Database=${DB_NAME};Username=${DB_USER};Password=${DB_PASS}"
    ;;
  mysql|mariadb)
    cd "${MIGRATIONS_DIR}"
    dotnet ef database update \
        -- --globalSettings:databaseProvider=mysql \
           --globalSettings:mySql:connectionString="Server=localhost;Port=${DB_PORT};Database=${DB_NAME};Uid=${DB_USER};Pwd=${DB_PASS};"
    ;;
  mssql)
    cd "${MIGRATIONS_DIR}"
    dotnet run -- \
        "Server=localhost;Database=${DB_NAME};User Id=${DB_USER};Password=${DB_PASS};TrustServerCertificate=true;"
    ;;
esac

# --- Seed ---
echo "==> Seeding database with preset: ${PRESET_NAME}"
cd "${SEEDER_DIR}"
case "${DB_TYPE}" in
  postgres)
    dotnet run --project . -- preset --name "${PRESET_NAME}"
    ;;
  mysql|mariadb)
    globalSettings__databaseProvider=mySQL \
    globalSettings__mySql__connectionString="Server=localhost;Port=${DB_PORT};Database=${DB_NAME};Uid=${DB_USER};Pwd=${DB_PASS};" \
    dotnet run --project . -- preset --name "${PRESET_NAME}"
    ;;
  mssql)
    globalSettings__databaseProvider=sqlServer \
    globalSettings__sqlServer__connectionString="Server=localhost;Database=${DB_NAME};User Id=${DB_USER};Password=${DB_PASS};TrustServerCertificate=true;" \
    dotnet run --project . -- preset --name "${PRESET_NAME}"
    ;;
esac

# --- Write metadata, dump, cleanup ---
case "${DB_TYPE}" in
  postgres)
    docker exec "${CONTAINER_NAME}" \
        pg_dump --no-owner --no-acl -U "${DB_USER}" -d "${DB_NAME}" > "${DOCKER_DIR}/seed.sql"

    cat >> "${DOCKER_DIR}/seed.sql" << EOF

-- Seeder metadata
CREATE TABLE IF NOT EXISTS "_SeederMetadata" ("Key" text PRIMARY KEY, "Value" text);
INSERT INTO "_SeederMetadata" VALUES ('preset', '${PRESET_NAME}') ON CONFLICT ("Key") DO UPDATE SET "Value" = EXCLUDED."Value";
INSERT INTO "_SeederMetadata" VALUES ('git_sha', '${GIT_SHA}') ON CONFLICT ("Key") DO UPDATE SET "Value" = EXCLUDED."Value";
INSERT INTO "_SeederMetadata" VALUES ('built_at', '${BUILD_DATE}') ON CONFLICT ("Key") DO UPDATE SET "Value" = EXCLUDED."Value";
EOF
    ;;

  mysql|mariadb)
    docker exec "${CONTAINER_NAME}" \
        sh -c 'mysqldump -u root -p"'"${DB_PASS}"'" --no-tablespaces "'"${DB_NAME}"'" 2>/dev/null || mariadb-dump -u root -p"'"${DB_PASS}"'" --no-tablespaces "'"${DB_NAME}"'" 2>/dev/null' > "${DOCKER_DIR}/seed.sql"

    cat >> "${DOCKER_DIR}/seed.sql" << EOF

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
    docker cp "${CONTAINER_NAME}:/var/opt/mssql/data/${DB_NAME}.mdf" "${DOCKER_DIR}/${DB_NAME}.mdf"
    docker cp "${CONTAINER_NAME}:/var/opt/mssql/data/${DB_NAME}_log.ldf" "${DOCKER_DIR}/${DB_NAME}_log.ldf"
    ;;
esac

echo "==> Stopping ${DB_TYPE} container"
docker rm -f "${CONTAINER_NAME}"

_docker_build_and_push
echo "==> Done: ${PRESET_NAME} (${DB_TYPE}) → ${TAG}"
