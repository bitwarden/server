#!/bin/bash
# Starts SQL Server, waits for it to be ready, then attaches the seeded database files.
set -e

/opt/mssql/bin/sqlservr &
SQLSERVR_PID=$!

sqlcmd() {
    /opt/mssql-tools18/bin/sqlcmd -S localhost -U SA -P "${SA_PASSWORD}" -C "$@"
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

echo "Copying database files to data directory..."
cp /seed/vault_dev.mdf "${DATA_PATH}vault.mdf"
cp /seed/vault_dev_log.ldf "${DATA_PATH}vault_log.ldf"

echo "Attaching seeded database..."
sqlcmd -Q "CREATE DATABASE [vault] ON (FILENAME = '${DATA_PATH}vault.mdf'), (FILENAME = '${DATA_PATH}vault_log.ldf') FOR ATTACH"

echo "Attach complete."

wait "${SQLSERVR_PID}"
