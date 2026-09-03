#!/usr/bin/env bash
# Builds a seeded database Docker image for a given preset and database type, plus a
# "core bundle" tarball (data protection key + attachment blobs) under docker/bundles/
# that the consuming environment unpacks at /etc/bitwarden/core. See README.md.
#
# Usage:
#   ./build-seeded-image.sh <preset-name> [db-type]
#
#   db-type: postgres (default), mysql, mariadb, mssql
#
# Environment variables:
#   PUSH=true          Push images to ACR after build
#   REGISTRY           ACR registry (default: devimagesaedgdev.azurecr.io)
#   GIT_SHA            Override git SHA (default: current HEAD short SHA)
#   DP_KEY_XML         Data protection key XML content
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
REGISTRY="${REGISTRY:-devimagesaedgdev.azurecr.io}"
GIT_SHA="${GIT_SHA:-$(git rev-parse --short HEAD 2>/dev/null || echo 'unknown')}"
BUILD_DATE="$(date -u +"%Y-%m-%dT%H:%M:%SZ")"
PUSH="${PUSH:-false}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SEEDER_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"
REPO_ROOT="$(cd "${SEEDER_DIR}/../.." && pwd)"
DOCKER_DIR="${SEEDER_DIR}/docker/${DB_TYPE}"

# --- Validate DB type ---
case "${DB_TYPE}" in
  postgres|mysql|mariadb|mssql) ;;
  *)
    echo "ERROR: Unknown database type '${DB_TYPE}'. Supported: postgres, mysql, mariadb, mssql"
    exit 1
    ;;
esac

# Sanitize preset name for Docker tag + container name: replace dots with dashes
TAG="${PRESET_NAME//./-}"
IMAGE_REPO="${REGISTRY}/seeded-${DB_TYPE}"
IMAGE_VERSIONED="${IMAGE_REPO}:${TAG}-${GIT_SHA}"
IMAGE_LATEST="${IMAGE_REPO}:${TAG}-latest"

CONTAINER_NAME="seeder-build-${DB_TYPE}-${TAG}"
WORK_DIR="${DOCKER_DIR}/build/${TAG}"

# --- Cleanup on any exit (partial failures shouldn't leave containers behind) ---
cleanup() {
    local status=$?
    docker rm -f "${CONTAINER_NAME}" >/dev/null 2>&1 || true
    if [[ "${KEEP_BUILD_DIR:-0}" != "1" ]]; then
        # BUNDLE_STAGE holds key material
        rm -rf "${WORK_DIR}" "${DOCKER_DIR}/build/${TAG}-bundle"
    fi
    return "${status}"
}
trap cleanup EXIT

echo "==> Building seeded ${DB_TYPE} image for preset: ${PRESET_NAME}"
echo "    Versioned: ${IMAGE_VERSIONED}"
echo "    Latest:    ${IMAGE_LATEST}"
echo "    Git SHA:   ${GIT_SHA}"
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
        --build-arg "GIT_SHA=${GIT_SHA}" \
        --build-arg "BUILD_DATE=${BUILD_DATE}" \
        --build-arg "OWNER_EMAIL=${OWNER_EMAIL}" \
        -t "${IMAGE_VERSIONED}" \
        -t "${IMAGE_LATEST}" \
        "${WORK_DIR}" \
        --load

    echo "==> Built: ${IMAGE_VERSIONED}"
    echo "==> Built: ${IMAGE_LATEST}"

    if [[ "${PUSH}" == "true" ]]; then
        # Caller is responsible for registry auth (e.g. `az acr login` in CI or
        # locally) before invoking with PUSH=true.
        echo "==> Pushing images"
        docker push "${IMAGE_VERSIONED}"
        docker push "${IMAGE_LATEST}"
        echo "==> Pushed: ${IMAGE_VERSIONED}"
        echo "==> Pushed: ${IMAGE_LATEST}"

        # free up disk after push
        docker rmi "${IMAGE_VERSIONED}" "${IMAGE_LATEST}" >/dev/null 2>&1 || true
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
esac

# --- Core bundle ---
# Data protection keys and attachment blobs, tarred for the consumer to unpack at
# /etc/bitwarden/core. Staged outside WORK_DIR, which is the Docker build context.
BUNDLE_STAGE="${DOCKER_DIR}/build/${TAG}-bundle"
CORE_DIR="${BUNDLE_STAGE}/core"
DP_KEYS_DIR="${CORE_DIR}/aspnet-dataprotection"
ATTACHMENTS_DIR="${CORE_DIR}/attachments"
BUNDLE_DIR="${SEEDER_DIR}/docker/bundles"
BUNDLE_FILE="${BUNDLE_DIR}/seeded-core-${DB_TYPE}-${TAG}-${GIT_SHA}.tar.gz"
mkdir -p "${DP_KEYS_DIR}" "${ATTACHMENTS_DIR}" "${BUNDLE_DIR}"

DP_KEY_FILENAME="key-9aa06f19-9afe-414b-8791-189be3b5650f.xml"
DP_KEY_SRC="${SEEDER_DIR}/docker/dp-keys/${DP_KEY_FILENAME}"

if [[ -n "${DP_KEY_XML:-}" ]]; then
    echo "==> Using data protection key from DP_KEY_XML"
    echo "${DP_KEY_XML}" > "${DP_KEYS_DIR}/${DP_KEY_FILENAME}"
elif [[ -f "${DP_KEY_SRC}" ]]; then
    echo "==> Using data protection key from ${DP_KEY_SRC}"
    cp "${DP_KEY_SRC}" "${DP_KEYS_DIR}/"
else
    echo "ERROR: No data protection key. Set DP_KEY_XML or place a key at ${DP_KEY_SRC}."
    exit 1
fi

# Self-hosted mode uses the licensing certificates embedded in Core and a no-op event
# repository. Installation ID is required when self-hosted. A blank attachment
# connection string selects local disk over Azure.
SEED_ENV=(
    "globalSettings__selfHosted=true"
    "globalSettings__installation__id=e6b8a9c4-0d3f-4a71-9c2e-5f7a1b3d8e02"
    "globalSettings__dataProtection__directory=${DP_KEYS_DIR}"
    "globalSettings__attachment__connectionString="
    "globalSettings__attachment__baseDirectory=${ATTACHMENTS_DIR}"
)

_write_core_bundle() {
    tar -czf "${BUNDLE_FILE}" -C "${BUNDLE_STAGE}" core
    echo "==> Core bundle: ${BUNDLE_FILE}"
    echo "    Unpack with: tar -xzf $(basename "${BUNDLE_FILE}") -C /etc/bitwarden"
}

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
        mcr.microsoft.com/mssql/server:2025-CU5-ubuntu-24.04 >/dev/null
    ;;
esac

# Poll for the published host port. `with` yields an empty string while unbound.
for _ in $(seq 1 30); do
    HOST_PORT=$(docker inspect \
        --format="{{with index .NetworkSettings.Ports \"${INTERNAL_PORT}/tcp\"}}{{(index . 0).HostPort}}{{end}}" \
        "${CONTAINER_NAME}")
    [[ -n "${HOST_PORT}" ]] && break
    sleep 1
done

if [[ -z "${HOST_PORT}" ]]; then
    echo "ERROR: ${DB_TYPE} container never published port ${INTERNAL_PORT}"
    docker logs --tail 50 "${CONTAINER_NAME}" || true
    exit 1
fi
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
    DB_PROVIDER="postgreSql"
    DB_CONNECTION="globalSettings__postgreSql__connectionString=Host=localhost;Port=${HOST_PORT};Database=${DB_NAME};Username=${DB_USER};Password=${DB_PASS}"
    ;;
  mysql|mariadb)
    DB_PROVIDER="mySQL"
    DB_CONNECTION="globalSettings__mySql__connectionString=Server=localhost;Port=${HOST_PORT};Database=${DB_NAME};Uid=${DB_USER};Pwd=${DB_PASS};"
    ;;
  mssql)
    DB_PROVIDER="sqlServer"
    DB_CONNECTION="globalSettings__sqlServer__connectionString=Server=localhost,${HOST_PORT};Database=${DB_NAME};User Id=${DB_USER};Password=${DB_PASS};TrustServerCertificate=true;"
    ;;
esac

SEED_LOG="$(mktemp)"
env "${SEED_ENV[@]}" \
    "globalSettings__databaseProvider=${DB_PROVIDER}" \
    "${DB_CONNECTION}" \
    dotnet run --project . -- preset --name "${PRESET_NAME}" | tee "${SEED_LOG}"

OWNER_EMAIL=$(grep -E '^\s*(Owner|Email)\s*:' "${SEED_LOG}" | head -1 | sed -E 's/^\s*(Owner|Email)\s*:\s*//')
rm -f "${SEED_LOG}"

# --- Dump database ---
case "${DB_TYPE}" in
  postgres)
    docker exec "${CONTAINER_NAME}" \
        pg_dump --no-owner --no-acl -U "${DB_USER}" -d "${DB_NAME}" > "${WORK_DIR}/seed.sql"
    ;;

  mysql|mariadb)
    docker exec "${CONTAINER_NAME}" \
        sh -c 'mysqldump -u root -p"'"${DB_PASS}"'" --no-tablespaces "'"${DB_NAME}"'" 2>/dev/null || mariadb-dump -u root -p"'"${DB_PASS}"'" --no-tablespaces "'"${DB_NAME}"'" 2>/dev/null' > "${WORK_DIR}/seed.sql"
    ;;

  mssql)
    # Copy MDF/LDF files directly — avoids RESTORE issues on Kubernetes PVCs
    docker exec "${CONTAINER_NAME}" \
        /opt/mssql-tools18/bin/sqlcmd \
            -S localhost -U SA -P "${DB_PASS}" -C -b \
            -Q "ALTER DATABASE [${DB_NAME}] SET OFFLINE WITH ROLLBACK IMMEDIATE"
    docker cp "${CONTAINER_NAME}:/var/opt/mssql/data/${DB_NAME}.mdf" "${WORK_DIR}/${DB_NAME}.mdf"
    docker cp "${CONTAINER_NAME}:/var/opt/mssql/data/${DB_NAME}_log.ldf" "${WORK_DIR}/${DB_NAME}_log.ldf"
    ;;
esac

echo "==> Stopping ${DB_TYPE} container"
docker rm -f "${CONTAINER_NAME}" >/dev/null

_write_core_bundle
_docker_build_and_push
echo "==> Done: ${PRESET_NAME} (${DB_TYPE}) → ${TAG}"
