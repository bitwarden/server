using Npgsql;
using NpgsqlTypes;
using Bit.Seeder.Migration.Models;
using Bit.Seeder.Migration.Utils;
using Microsoft.Extensions.Logging;

namespace Bit.Seeder.Migration.Databases;

/// <summary>
/// PostgreSQL database importer that handles schema creation, data import, and constraint management.
/// </summary>
public class PostgresImporter(DatabaseConfig config, ILogger<PostgresImporter> logger) : IDatabaseImporter
{
    private readonly ILogger<PostgresImporter> _logger = logger;
    private readonly string _host = config.Host;
    private readonly int _port = config.Port > 0 ? config.Port : 5432;
    private readonly string _database = config.Database;
    private readonly string _username = config.Username;
    private readonly string _password = config.Password;
    private NpgsqlConnection? _connection;
    private bool _disposed = false;

    /// <summary>
    /// Connects to the PostgreSQL database.
    /// </summary>
    public bool Connect()
    {
        try
        {
            var safeConnectionString = $"Host={_host};Port={_port};Database={_database};" +
                                      $"Username={_username};Password={DbSeederConstants.REDACTED_PASSWORD};" +
                                      $"Timeout={DbSeederConstants.DEFAULT_CONNECTION_TIMEOUT};CommandTimeout={DbSeederConstants.DEFAULT_COMMAND_TIMEOUT};";

            var actualConnectionString = safeConnectionString.Replace(DbSeederConstants.REDACTED_PASSWORD, _password);

            _connection = new NpgsqlConnection(actualConnectionString);
            _connection.Open();

            _logger.LogInformation("Connected to PostgreSQL: {Host}:{Port}/{Database}", _host, _port, _database);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to connect to PostgreSQL: {Message}", ex.Message);
            return false;
        }
    }

    public void Disconnect()
    {
        if (_connection != null)
        {
            _connection.Close();
            _connection.Dispose();
            _connection = null;
            _logger.LogInformation("Disconnected from PostgreSQL");
        }
    }

    /// <summary>
    /// Creates a table from the provided schema definition.
    /// </summary>
    public bool CreateTableFromSchema(
        string tableName,
        List<string> columns,
        Dictionary<string, string> columnTypes,
        List<string>? specialColumns = null)
    {
        specialColumns ??= [];

        if (_connection == null)
            throw new InvalidOperationException("Not connected to database");

        IdentifierValidator.ValidateOrThrow(tableName, "table name");

        try
        {
            // Convert SQL Server types to PostgreSQL types
            var pgColumns = new List<string>();
            foreach (var colName in columns)
            {
                IdentifierValidator.ValidateOrThrow(colName, "column name");

                var sqlServerType = columnTypes.GetValueOrDefault(colName, "VARCHAR(MAX)");
                var pgType = ConvertSqlServerTypeToPostgreSQL(sqlServerType, specialColumns.Contains(colName));
                pgColumns.Add($"\"{colName}\" {pgType}");
            }

            // Create tables with quoted identifiers to preserve case
            var createSql = $@"
                CREATE TABLE IF NOT EXISTS ""{tableName}"" (
                    {string.Join(",\n                    ", pgColumns)}
                )";

            _logger.LogInformation("Creating table {TableName} in PostgreSQL", tableName);
            _logger.LogDebug("CREATE TABLE SQL: {CreateSql}", createSql);

            using var command = new NpgsqlCommand(createSql, _connection);
            command.ExecuteNonQuery();

            _logger.LogInformation("Successfully created table {TableName}", tableName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError("Error creating table {TableName}: {Message}", tableName, ex.Message);
            return false;
        }
    }

    private string? GetActualTableName(string tableName)
    {
        if (_connection == null)
            throw new InvalidOperationException("Not connected to database");

        try
        {
            var query = @"
                SELECT table_name
                FROM information_schema.tables
                WHERE LOWER(table_name) = LOWER(@tableName) AND table_schema = 'public'
                LIMIT 1";

            using var command = new NpgsqlCommand(query, _connection);
            command.Parameters.AddWithValue("tableName", tableName);

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                return reader.GetString(0);
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError("Error getting actual table name for {TableName}: {Message}", tableName, ex.Message);
            return null;
        }
    }

    public List<string> GetTableColumns(string tableName)
    {
        if (_connection == null)
            throw new InvalidOperationException("Not connected to database");

        try
        {
            var query = @"
                SELECT column_name
                FROM information_schema.columns
                WHERE LOWER(table_name) = LOWER(@tableName) AND table_schema = 'public'
                ORDER BY ordinal_position";

            using var command = new NpgsqlCommand(query, _connection);
            command.Parameters.AddWithValue("tableName", tableName);

            var columns = new List<string>();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                columns.Add(reader.GetString(0));
            }

            return columns;
        }
        catch (Exception ex)
        {
            _logger.LogError("Error getting columns for table {TableName}: {Message}", tableName, ex.Message);
            return [];
        }
    }

    private Dictionary<string, string> GetTableColumnTypes(string tableName)
    {
        if (_connection == null)
            throw new InvalidOperationException("Not connected to database");

        try
        {
            var columnTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var query = @"
                SELECT column_name, data_type
                FROM information_schema.columns
                WHERE LOWER(table_name) = LOWER(@tableName) AND table_schema = 'public'";

            using var command = new NpgsqlCommand(query, _connection);
            command.Parameters.AddWithValue("tableName", tableName);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                columnTypes[reader.GetString(0)] = reader.GetString(1);
            }

            return columnTypes;
        }
        catch (Exception ex)
        {
            _logger.LogError("Error getting column types for table {TableName}: {Message}", tableName, ex.Message);
            return new Dictionary<string, string>();
        }
    }

    /// <summary>
    /// Imports data into a table using batch INSERT statements.
    /// </summary>
    public bool ImportData(
        string tableName,
        List<string> columns,
        List<object[]> data,
        int batchSize = DbSeederConstants.DEFAULT_BATCH_SIZE)
    {
        if (_connection == null)
            throw new InvalidOperationException("Not connected to database");

        IdentifierValidator.ValidateOrThrow(tableName, "table name");

        if (data.Count == 0)
        {
            _logger.LogWarning("No data to import for table {TableName}", tableName);
            return true;
        }

        try
        {
            // Get the actual table name with correct casing
            var actualTableName = GetActualTableName(tableName);
            if (actualTableName == null)
            {
                _logger.LogError("Table {TableName} not found in database", tableName);
                return false;
            }

            IdentifierValidator.ValidateOrThrow(actualTableName, "actual table name");

            var actualColumns = GetTableColumns(tableName);
            if (actualColumns.Count == 0)
            {
                _logger.LogError("Could not retrieve columns for table {TableName}", tableName);
                return false;
            }

            // Get column types from the database
            var columnTypes = GetTableColumnTypes(tableName);

            // Filter columns - use case-insensitive comparison
            var validColumnIndices = new List<int>();
            var validColumns = new List<string>();
            var validColumnTypes = new List<string>();

            // Create a case-insensitive lookup of actual columns
            var actualColumnsLookup = actualColumns.ToDictionary(c => c, c => c, StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < columns.Count; i++)
            {
                if (actualColumnsLookup.TryGetValue(columns[i], out var actualColumnName))
                {
                    validColumnIndices.Add(i);
                    validColumns.Add(actualColumnName); // Use the actual column name from DB
                    validColumnTypes.Add(columnTypes.GetValueOrDefault(actualColumnName, "text"));
                }
                else
                {
                    _logger.LogDebug("Column '{Column}' from CSV not found in table {TableName}", columns[i], tableName);
                }
            }

            if (validColumns.Count == 0)
            {
                _logger.LogError("No valid columns found for table {TableName}", tableName);
                _logger.LogError("CSV columns: {Columns}", string.Join(", ", columns));
                _logger.LogError("Table columns: {Columns}", string.Join(", ", actualColumns));
                return false;
            }

            var filteredData = data.Select(row =>
                validColumnIndices.Select(i => i < row.Length ? row[i] : null).ToArray()
            ).ToList();

            _logger.LogInformation("Importing {Count} rows into {TableName}", filteredData.Count, tableName);

            // Build INSERT statement with explicit type casts for all types
            var quotedColumns = validColumns.Select(col => $"\"{col}\"").ToList();
            var placeholders = validColumns.Select((col, idx) =>
            {
                var paramNum = idx + 1;
                var colType = validColumnTypes[idx];
                // Cast to appropriate type if needed - PostgreSQL requires explicit casts for text to other types
                return colType switch
                {
                    // UUID types
                    "uuid" => $"${paramNum}::uuid",

                    // Timestamp types
                    "timestamp without time zone" => $"${paramNum}::timestamp",
                    "timestamp with time zone" => $"${paramNum}::timestamptz",
                    "date" => $"${paramNum}::date",
                    "time without time zone" => $"${paramNum}::time",
                    "time with time zone" => $"${paramNum}::timetz",

                    // Integer types
                    "smallint" => $"${paramNum}::smallint",
                    "integer" => $"${paramNum}::integer",
                    "bigint" => $"${paramNum}::bigint",

                    // Numeric types
                    "numeric" => $"${paramNum}::numeric",
                    "decimal" => $"${paramNum}::decimal",
                    "real" => $"${paramNum}::real",
                    "double precision" => $"${paramNum}::double precision",

                    // Boolean type
                    "boolean" => $"${paramNum}::boolean",

                    // Default - no cast needed for text types
                    _ => $"${paramNum}"
                };
            });
            var insertSql = $"INSERT INTO \"{actualTableName}\" ({string.Join(", ", quotedColumns)}) VALUES ({string.Join(", ", placeholders)})";

            var totalImported = 0;
            for (int i = 0; i < filteredData.Count; i += batchSize)
            {
                var batch = filteredData.Skip(i).Take(batchSize).ToList();

                using var transaction = _connection.BeginTransaction();
                try
                {
                    foreach (var row in batch)
                    {
                        using var command = new NpgsqlCommand(insertSql, _connection, transaction);

                        var preparedRow = PrepareRowForInsert(row, validColumns);
                        for (int p = 0; p < preparedRow.Length; p++)
                        {
                            command.Parameters.AddWithValue(preparedRow[p] ?? DBNull.Value);
                        }

                        command.ExecuteNonQuery();
                    }

                    transaction.Commit();
                    totalImported += batch.Count;

                    if (filteredData.Count > DbSeederConstants.LOGGING_THRESHOLD)
                    {
                        _logger.LogDebug("Batch: {BatchCount} rows ({TotalImported}/{FilteredDataCount} total)", batch.Count, totalImported, filteredData.Count);
                    }
                }
                catch
                {
                    transaction.SafeRollback(_connection, _logger, tableName);
                    throw;
                }
            }

            _logger.LogInformation("Successfully imported {TotalImported} rows into {TableName}", totalImported, tableName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError("Error importing data into {TableName}: {Message}", tableName, ex.Message);
            _logger.LogError("Stack trace: {StackTrace}", ex.StackTrace);
            if (ex.InnerException != null)
            {
                _logger.LogError("Inner exception: {Message}", ex.InnerException.Message);
            }
            return false;
        }
    }

    /// <summary>
    /// Checks if a table exists in the database.
    /// </summary>
    public bool TableExists(string tableName)
    {
        if (_connection == null)
            throw new InvalidOperationException("Not connected to database");

        IdentifierValidator.ValidateOrThrow(tableName, "table name");

        try
        {
            var query = @"
                SELECT EXISTS (
                    SELECT 1 FROM information_schema.tables
                    WHERE LOWER(table_name) = LOWER(@tableName) AND table_schema = 'public'
                )";

            using var command = new NpgsqlCommand(query, _connection);
            command.Parameters.AddWithValue("tableName", tableName);

            return command.GetScalarValue<bool>(false, _logger, $"table existence check for {tableName}");
        }
        catch (Exception ex)
        {
            _logger.LogError("Error checking if table {TableName} exists: {Message}", tableName, ex.Message);
            return false;
        }
    }

    public int GetTableRowCount(string tableName)
    {
        if (_connection == null)
            throw new InvalidOperationException("Not connected to database");

        try
        {
            var actualTableName = GetActualTableName(tableName);
            if (actualTableName == null)
            {
                _logger.LogError("Table {TableName} not found in database", tableName);
                return 0;
            }

            var query = $"SELECT COUNT(*) FROM \"{actualTableName}\"";
            using var command = new NpgsqlCommand(query, _connection);

            return Convert.ToInt32(command.ExecuteScalar());
        }
        catch (Exception ex)
        {
            _logger.LogError("Error getting row count for {TableName}: {Message}", tableName, ex.Message);
            return 0;
        }
    }

    public bool DropTable(string tableName)
    {
        if (_connection == null)
            throw new InvalidOperationException("Not connected to database");

        try
        {
            var actualTableName = GetActualTableName(tableName);
            if (actualTableName == null)
            {
                _logger.LogWarning("Table {TableName} not found, skipping drop", tableName);
                return true;
            }

            var query = $"DROP TABLE IF EXISTS \"{actualTableName}\" CASCADE";
            using var command = new NpgsqlCommand(query, _connection);
            command.ExecuteNonQuery();

            _logger.LogInformation("Dropped table {TableName}", tableName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError("Error dropping table {TableName}: {Message}", tableName, ex.Message);
            return false;
        }
    }

    public bool DisableForeignKeys()
    {
        if (_connection == null)
            throw new InvalidOperationException("Not connected to database");

        try
        {
            _logger.LogInformation("Disabling foreign key constraints");
            var query = "SET session_replication_role = replica;";
            using var command = new NpgsqlCommand(query, _connection);
            command.ExecuteNonQuery();

            _logger.LogInformation("Foreign key constraints deferred");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError("Error disabling foreign key constraints: {Message}", ex.Message);
            return false;
        }
    }

    public bool EnableForeignKeys()
    {
        if (_connection == null)
            throw new InvalidOperationException("Not connected to database");

        try
        {
            _logger.LogInformation("Re-enabling foreign key constraints");
            var query = "SET session_replication_role = DEFAULT;";
            using var command = new NpgsqlCommand(query, _connection);
            command.ExecuteNonQuery();

            _logger.LogInformation("Foreign key constraints re-enabled");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError("Error re-enabling foreign key constraints: {Message}", ex.Message);
            return false;
        }
    }

    private string ConvertSqlServerTypeToPostgreSQL(string sqlServerType, bool isJsonColumn)
    {
        var baseType = sqlServerType.Replace(" NULL", "").Replace(" NOT NULL", "").Trim();
        var isNullable = !sqlServerType.Contains("NOT NULL");

        if (isJsonColumn)
            return "TEXT" + (isNullable ? "" : " NOT NULL");

        var pgType = baseType.ToUpper() switch
        {
            var t when t.StartsWith("VARCHAR") => t.Contains("MAX") ? "TEXT" : t.Replace("VARCHAR", "VARCHAR"),
            var t when t.StartsWith("NVARCHAR") => "TEXT",
            "INT" or "INTEGER" => "INTEGER",
            "BIGINT" => "BIGINT",
            "SMALLINT" => "SMALLINT",
            "TINYINT" => "SMALLINT",
            "BIT" => "BOOLEAN",
            var t when t.StartsWith("DECIMAL") => t.Replace("DECIMAL", "DECIMAL"),
            "FLOAT" => "DOUBLE PRECISION",
            "REAL" => "REAL",
            "DATETIME" or "DATETIME2" or "SMALLDATETIME" => "TIMESTAMP",
            "DATE" => "DATE",
            "TIME" => "TIME",
            "DATETIMEOFFSET" => "TIMESTAMPTZ",
            "UNIQUEIDENTIFIER" => "UUID",
            var t when t.StartsWith("VARBINARY") => "BYTEA",
            "XML" => "XML",
            _ => "TEXT"
        };

        return pgType + (isNullable ? "" : " NOT NULL");
    }

    private object[] PrepareRowForInsert(object?[] row, List<string> columns)
    {
        return row.Select(value =>
        {
            if (value == null || value == DBNull.Value)
                return DBNull.Value;

            if (value is string strValue)
            {
                // Only convert truly empty strings to DBNull, not whitespace
                // This preserves JSON strings and other data that might have whitespace
                if (strValue.Length == 0)
                    return DBNull.Value;

                if (strValue.Equals("true", StringComparison.OrdinalIgnoreCase))
                    return true;
                if (strValue.Equals("false", StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return value;
        }).ToArray();
    }

    public bool SupportsBulkCopy()
    {
        return true; // PostgreSQL COPY is highly optimized
    }

    public bool ImportDataBulk(
        string tableName,
        List<string> columns,
        List<object[]> data)
    {
        if (_connection == null)
            throw new InvalidOperationException("Not connected to database");

        if (data.Count == 0)
        {
            _logger.LogWarning("No data to import for table {TableName}", tableName);
            return true;
        }

        try
        {
            // Get the actual table name with correct casing
            var actualTableName = GetActualTableName(tableName);
            if (actualTableName == null)
            {
                _logger.LogError("Table {TableName} not found in database", tableName);
                return false;
            }

            var actualColumns = GetTableColumns(tableName);
            if (actualColumns.Count == 0)
            {
                _logger.LogError("Could not retrieve columns for table {TableName}", tableName);
                return false;
            }

            // Get column types from the database
            var columnTypes = GetTableColumnTypes(tableName);

            // Filter columns - use case-insensitive comparison
            var validColumnIndices = new List<int>();
            var validColumns = new List<string>();
            var validColumnTypes = new List<string>();

            // Create a case-insensitive lookup of actual columns
            var actualColumnsLookup = actualColumns.ToDictionary(c => c, c => c, StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < columns.Count; i++)
            {
                if (actualColumnsLookup.TryGetValue(columns[i], out var actualColumnName))
                {
                    validColumnIndices.Add(i);
                    validColumns.Add(actualColumnName);
                    validColumnTypes.Add(columnTypes.GetValueOrDefault(actualColumnName, "text"));
                }
                else
                {
                    _logger.LogDebug("Column '{Column}' from CSV not found in table {TableName}", columns[i], tableName);
                }
            }

            if (validColumns.Count == 0)
            {
                _logger.LogError("No valid columns found for table {TableName}", tableName);
                return false;
            }

            var filteredData = data.Select(row =>
                validColumnIndices.Select(i => i < row.Length ? row[i] : null).ToArray()
            ).ToList();

            _logger.LogInformation("Bulk importing {Count} rows into {TableName} using PostgreSQL COPY", filteredData.Count, tableName);

            // Use PostgreSQL's COPY command for binary import (fastest method)
            var quotedColumns = validColumns.Select(col => $"\"{col}\"");
            var copyCommand = $"COPY \"{actualTableName}\" ({string.Join(", ", quotedColumns)}) FROM STDIN (FORMAT BINARY)";

            using var writer = _connection.BeginBinaryImport(copyCommand);

            foreach (var row in filteredData)
            {
                writer.StartRow();

                var preparedRow = PrepareRowForInsert(row, validColumns);
                for (int i = 0; i < preparedRow.Length; i++)
                {
                    var value = preparedRow[i];

                    if (value == null || value == DBNull.Value)
                    {
                        writer.WriteNull();
                    }
                    else
                    {
                        // Write with appropriate type based on column type
                        var colType = validColumnTypes[i];
                        WriteValueForCopy(writer, value, colType);
                    }
                }
            }

            var rowsImported = writer.Complete();
            _logger.LogInformation("Successfully bulk imported {RowsImported} rows into {TableName}", rowsImported, tableName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError("Error during bulk import into {TableName}: {Message}", tableName, ex.Message);
            _logger.LogError("Stack trace: {StackTrace}", ex.StackTrace);
            if (ex.InnerException != null)
            {
                _logger.LogError("Inner exception: {Message}", ex.InnerException.Message);
            }
            return false;
        }
    }

    private void WriteValueForCopy(Npgsql.NpgsqlBinaryImporter writer, object value, string columnType)
    {
        // Handle type-specific writing for PostgreSQL COPY
        switch (columnType.ToLower())
        {
            case "uuid":
                if (value is string strGuid && Guid.TryParse(strGuid, out var guid))
                    writer.Write(guid, NpgsqlDbType.Uuid);
                else if (value is Guid g)
                    writer.Write(g, NpgsqlDbType.Uuid);
                else
                    writer.Write(value.ToString()!, NpgsqlDbType.Uuid);
                break;

            case "boolean":
                if (value is bool b)
                    writer.Write(b);
                else if (value is string strBool)
                    writer.Write(strBool.Equals("true", StringComparison.OrdinalIgnoreCase) || strBool == "1");
                else
                    writer.Write(Convert.ToBoolean(value));
                break;

            case "smallint":
                writer.Write(Convert.ToInt16(value));
                break;

            case "integer":
                writer.Write(Convert.ToInt32(value));
                break;

            case "bigint":
                writer.Write(Convert.ToInt64(value));
                break;

            case "real":
                writer.Write(Convert.ToSingle(value));
                break;

            case "double precision":
                writer.Write(Convert.ToDouble(value));
                break;

            case "numeric":
            case "decimal":
                writer.Write(Convert.ToDecimal(value));
                break;

            case "timestamp without time zone":
            case "timestamp":
                if (value is DateTime dt)
                {
                    // For timestamp without time zone, we can use the value as-is
                    // But if it's Unspecified, treat it as if it's in the local context
                    var timestampValue = dt.Kind == DateTimeKind.Unspecified
                        ? DateTime.SpecifyKind(dt, DateTimeKind.Utc)
                        : dt;
                    writer.Write(timestampValue, NpgsqlDbType.Timestamp);
                }
                else if (value is string strDt && DateTime.TryParse(strDt, out var parsedDt))
                {
                    var timestampValue = DateTime.SpecifyKind(parsedDt, DateTimeKind.Utc);
                    writer.Write(timestampValue, NpgsqlDbType.Timestamp);
                }
                else
                    writer.Write(value.ToString()!);
                break;

            case "timestamp with time zone":
            case "timestamptz":
                if (value is DateTime dtz)
                {
                    // PostgreSQL timestamptz requires UTC DateTimes
                    var utcValue = dtz.Kind == DateTimeKind.Unspecified
                        ? DateTime.SpecifyKind(dtz, DateTimeKind.Utc)
                        : dtz.Kind == DateTimeKind.Local
                            ? dtz.ToUniversalTime()
                            : dtz;
                    writer.Write(utcValue, NpgsqlDbType.TimestampTz);
                }
                else if (value is string strDtz && DateTime.TryParse(strDtz, out var parsedDtz))
                {
                    // Parsed DateTimes are Unspecified, treat as UTC
                    var utcValue = DateTime.SpecifyKind(parsedDtz, DateTimeKind.Utc);
                    writer.Write(utcValue, NpgsqlDbType.TimestampTz);
                }
                else
                    writer.Write(value.ToString()!);
                break;

            case "date":
                if (value is DateTime date)
                    writer.Write(date, NpgsqlDbType.Date);
                else if (value is string strDate && DateTime.TryParse(strDate, out var parsedDate))
                    writer.Write(parsedDate, NpgsqlDbType.Date);
                else
                    writer.Write(value.ToString()!);
                break;

            case "bytea":
                if (value is byte[] bytes)
                    writer.Write(bytes);
                else
                    writer.Write(value.ToString()!);
                break;

            default:
                // Text and all other types
                writer.Write(value.ToString()!);
                break;
        }
    }

    /// <summary>
    /// Tests the connection to PostgreSQL by executing a simple query.
    /// </summary>
    public bool TestConnection()
    {
        try
        {
            if (Connect())
            {
                using var command = new NpgsqlCommand("SELECT 1", _connection);
                var result = command.GetScalarValue<int>(0, _logger, "connection test");
                Disconnect();
                return result == 1;
            }
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError("PostgreSQL connection test failed: {Message}", ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Disposes of the PostgreSQL importer and releases all resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Implements Dispose pattern for resource cleanup.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                Disconnect();
            }
            _disposed = true;
        }
    }
}
