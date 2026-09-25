#!/bin/bash
# Starts SQL Server, waits for it to be ready, then attaches the seeded database files.
set -e

/opt/mssql/bin/sqlservr &
SQLSERVR_PID=$!

# PID 1 discards SIGTERM by default, so forward it and let sqlservr shut down cleanly
term_handler() {
    kill -TERM "${SQLSERVR_PID}" 2>/dev/null || true
    wait "${SQLSERVR_PID}" || true
    exit 143
}
trap term_handler TERM INT

sqlcmd() {
    # MSSQL_SA_PASSWORD is what current images (and Aspire) set; SA_PASSWORD is the deprecated
    # name some consumers still use.
    /opt/mssql-tools18/bin/sqlcmd -S localhost -U SA -P "${MSSQL_SA_PASSWORD:-$SA_PASSWORD}" -C "$@"
}

# Polls the given command every 2s, failing the container if it never succeeds
wait_for() {
    local what="$1" attempts="$2"
    shift 2
    echo "Waiting for ${what}..."
    for _ in $(seq 1 "${attempts}"); do
        if "$@"; then
            echo "${what}: ready."
            return 0
        fi
        sleep 2
    done
    echo "ERROR: timed out waiting for ${what}"
    return 1
}

accepts_connections() {
    sqlcmd -Q "SELECT 1" > /dev/null 2>&1
}

system_databases_online() {
    local offline
    offline=$(sqlcmd -h -1 \
        -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.databases WHERE database_id <= 4 AND state_desc <> 'ONLINE'" \
        2>/dev/null | tr -d '[:space:]')
    [ "${offline}" = "0" ]
}

# Creating and dropping a database proves the data directory is writable
data_directory_writable() {
    sqlcmd -Q "CREATE DATABASE [__attach_ready]; DROP DATABASE [__attach_ready]" > /dev/null 2>&1
}

wait_for "SQL Server connections" 60 accepts_connections
wait_for "system databases online" 60 system_databases_online
wait_for "writable data directory" 30 data_directory_writable

DATA_PATH=$(sqlcmd -h -1 \
    -Q "SET NOCOUNT ON; SELECT CAST(SERVERPROPERTY('InstanceDefaultDataPath') AS NVARCHAR(512))" \
    2>/dev/null | tr -d '\r\n ')
echo "MSSQL default data path: ${DATA_PATH}"

database_exists() {
    local count
    count=$(sqlcmd -h -1 \
        -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.databases WHERE name = 'vault_dev'" \
        2>/dev/null | tr -d '[:space:]')
    [ "${count}" = "1" ]
}

# Counts rows, since a zero-row SELECT is not a sqlcmd error
database_seeded() {
    local count
    count=$(sqlcmd -b -h -1 -d vault_dev \
        -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM [User]" \
        2>/dev/null | tr -d '[:space:]')
    [ -n "${count}" ] && [ "${count}" -gt 0 ] 2>/dev/null
}

# The data directory is usually a mounted volume, so vault_dev survives a restart
if database_exists; then
    if ! database_seeded; then
        echo "ERROR: a 'vault_dev' database exists but holds no seeded data."
        echo "Something created it before this image could attach the seed. Start the database"
        echo "and wait for it to report healthy before starting anything that migrates."
        exit 1
    fi
    echo "Database 'vault_dev' is already attached. Leaving it as is."
else
    echo "Copying database files to data directory..."
    cp /seed/vault_dev.mdf "${DATA_PATH}vault_dev.mdf"
    cp /seed/vault_dev_log.ldf "${DATA_PATH}vault_dev_log.ldf"

    # -b exits non-zero on a T-SQL error so a failed attach does not log success
    echo "Attaching seeded database..."
    sqlcmd -b -Q "CREATE DATABASE [vault_dev] ON (FILENAME = '${DATA_PATH}vault_dev.mdf'), (FILENAME = '${DATA_PATH}vault_dev_log.ldf') FOR ATTACH"

    echo "Attach complete."
fi

wait "${SQLSERVR_PID}"
