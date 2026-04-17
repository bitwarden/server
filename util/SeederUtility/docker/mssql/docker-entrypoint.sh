#!/bin/bash
# Starts SQL Server, waits for it to be ready, then attaches the seeded database files.
set -e

# Start SQL Server in the background
/opt/mssql/bin/sqlservr &
MSSQL_PID=$!

# Wait for SQL Server to accept connections
echo "Waiting for SQL Server to start..."
for i in $(seq 1 60); do
    if /opt/mssql-tools18/bin/sqlcmd \
        -S localhost -U SA -P "${SA_PASSWORD}" -C \
        -Q "SELECT 1" > /dev/null 2>&1; then
        echo "SQL Server accepting connections."
        break
    fi
    sleep 2
done

# Wait for all system databases to be ONLINE
echo "Waiting for system databases to be fully online..."
for i in $(seq 1 60); do
    OFFLINE=$(/opt/mssql-tools18/bin/sqlcmd \
        -S localhost -U SA -P "${SA_PASSWORD}" -C -h -1 \
        -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.databases WHERE database_id <= 4 AND state_desc <> 'ONLINE'" \
        2>/dev/null | tr -d '[:space:]')
    if [ "${OFFLINE}" = "0" ]; then
        echo "All system databases online."
        break
    fi
    sleep 2
done

# Wait for data directory to be writable
echo "Waiting for data directory to be ready..."
for i in $(seq 1 30); do
    if /opt/mssql-tools18/bin/sqlcmd \
        -S localhost -U SA -P "${SA_PASSWORD}" -C \
        -Q "CREATE DATABASE [__attach_ready]; DROP DATABASE [__attach_ready]" \
        > /dev/null 2>&1; then
        echo "SQL Server ready."
        break
    fi
    sleep 2
done

# Resolve default data path
DATA_PATH=$(/opt/mssql-tools18/bin/sqlcmd \
    -S localhost -U SA -P "${SA_PASSWORD}" -C -h -1 \
    -Q "SET NOCOUNT ON; SELECT CAST(SERVERPROPERTY('InstanceDefaultDataPath') AS NVARCHAR(512))" \
    2>/dev/null | tr -d '\r\n ')
echo "MSSQL default data path: ${DATA_PATH}"

# Copy MDF/LDF files to the data directory
echo "Copying database files to data directory..."
cp /seed/vault_dev.mdf "${DATA_PATH}vault.mdf"
cp /seed/vault_dev_log.ldf "${DATA_PATH}vault_log.ldf"

# Attach the database
echo "Attaching seeded database..."
/opt/mssql-tools18/bin/sqlcmd \
    -S localhost \
    -U SA \
    -P "${SA_PASSWORD}" \
    -C \
    -Q "CREATE DATABASE [vault] ON (FILENAME = '${DATA_PATH}vault.mdf'), (FILENAME = '${DATA_PATH}vault_log.ldf') FOR ATTACH"

echo "Attach complete."

# Hand off to SQL Server process
wait $MSSQL_PID
