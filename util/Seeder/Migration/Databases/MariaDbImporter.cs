using MySqlConnector;
using Bit.Seeder.Migration.Models;
using Bit.Seeder.Migration.Utils;
using Microsoft.Extensions.Logging;

namespace Bit.Seeder.Migration.Databases;

/// <summary>
/// MariaDB database importer that handles schema creation, data import, and foreign key management.
/// </summary>
public class MariaDbImporter(DatabaseConfig config, ILogger<MariaDbImporter> logger) : IDatabaseImporter
{
    private readonly ILogger<MariaDbImporter> _logger = logger;
    private readonly string _host = config.Host;
    private readonly int _port = config.Port > 0 ? config.Port : 3306;
    private readonly string _database = config.Database;
    private readonly string _username = config.Username;
    private readonly string _password = config.Password;
    private MySqlConnection? _connection;
    private bool _disposed = false;

    /// <summary>
    /// Connects to the MariaDB database.
    /// </summary>
    public bool Connect()
    {
        try
        {
            // Build connection string with redacted password for safe logging
            var safeConnectionString = $"Server={_host};Port={_port};Database={_database};" +
                                      $"Uid={_username};Pwd={DbSeederConstants.REDACTED_PASSWORD};" +
                                      $"ConnectionTimeout={DbSeederConstants.DEFAULT_CONNECTION_TIMEOUT};" +
                                      $"CharSet=utf8mb4;AllowLoadLocalInfile=false;MaxPoolSize={DbSeederConstants.DEFAULT_MAX_POOL_SIZE};";

            var actualConnectionString = safeConnectionString.Replace(DbSeederConstants.REDACTED_PASSWORD, _password);

            _connection = new MySqlConnection(actualConnectionString);
            _connection.Open();

            _logger.LogInformation("Connected to MariaDB: {Host}:{Port}/{Database}", _host, _port, _database);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to connect to MariaDB: {Message}", ex.Message);
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
            _logger.LogInformation("Disconnected from MariaDB");
        }
    }

    /// <summary>
    /// Creates a table in MariaDB from the provided schema definition.
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
            var mariaColumns = new List<string>();
            foreach (var colName in columns)
            {
                IdentifierValidator.ValidateOrThrow(colName, "column name");

                var sqlServerType = columnTypes.GetValueOrDefault(colName, "VARCHAR(MAX)");
                var mariaType = ConvertSqlServerTypeToMariaDB(sqlServerType, specialColumns.Contains(colName));
                mariaColumns.Add($"`{colName}` {mariaType}");
            }

            var createSql = $@"
                CREATE TABLE IF NOT EXISTS `{tableName}` (
                    {string.Join(",\n                    ", mariaColumns)}
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci";

            _logger.LogInformation("Creating table {TableName} in MariaDB", tableName);
            _logger.LogDebug("CREATE TABLE SQL: {CreateSql}", createSql);

            using var command = new MySqlCommand(createSql, _connection);
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

    public List<string> GetTableColumns(string tableName)
    {
        if (_connection == null)
            throw new InvalidOperationException("Not connected to database");

        try
        {
            var query = @"
                SELECT column_name
                FROM information_schema.columns
                WHERE table_name = @tableName AND table_schema = @database
                ORDER BY ordinal_position";

            using var command = new MySqlCommand(query, _connection);
            command.Parameters.AddWithValue("@tableName", tableName);
            command.Parameters.AddWithValue("@database", _database);

            var columns = new List<string>();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var colName = reader.GetString(0);

                // Validate column name immediately to prevent second-order SQL injection
                IdentifierValidator.ValidateOrThrow(colName, "column name");

                columns.Add(colName);
            }

            return columns;
        }
        catch (Exception ex)
        {
            _logger.LogError("Error getting columns for table {TableName}: {Message}", tableName, ex.Message);
            return [];
        }
    }

    /// <summary>
    /// Imports data into a MariaDB table using batched INSERT statements.
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
            var actualColumns = GetTableColumns(tableName);
            if (actualColumns.Count == 0)
            {
                _logger.LogError("Could not retrieve columns for table {TableName}", tableName);
                return false;
            }

            var validColumnIndices = new List<int>();
            var validColumns = new List<string>();

            for (int i = 0; i < columns.Count; i++)
            {
                if (actualColumns.Contains(columns[i]))
                {
                    IdentifierValidator.ValidateOrThrow(columns[i], "column name");
                    validColumnIndices.Add(i);
                    validColumns.Add(columns[i]);
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

            _logger.LogInformation("Importing {Count} rows into {TableName}", filteredData.Count, tableName);

            // Build INSERT statement
            var quotedColumns = validColumns.Select(col => $"`{col}`").ToList();
            var placeholders = string.Join(", ", Enumerable.Range(0, validColumns.Count).Select(i => $"@p{i}"));
            var insertSql = $"INSERT INTO `{tableName}` ({string.Join(", ", quotedColumns)}) VALUES ({placeholders})";

            var totalImported = 0;
            for (int i = 0; i < filteredData.Count; i += batchSize)
            {
                var batch = filteredData.Skip(i).Take(batchSize).ToList();

                using var transaction = _connection.BeginTransaction();
                try
                {
                    foreach (var row in batch)
                    {
                        using var command = new MySqlCommand(insertSql, _connection, transaction);

                        var preparedRow = PrepareRowForInsert(row, validColumns);
                        for (int p = 0; p < preparedRow.Length; p++)
                        {
                            var value = preparedRow[p] ?? DBNull.Value;

                            // For string values, explicitly set parameter type and size to avoid truncation
                            if (value is string strValue)
                            {
                                var param = new MySqlConnector.MySqlParameter
                                {
                                    ParameterName = $"@p{p}",
                                    MySqlDbType = MySqlConnector.MySqlDbType.LongText,
                                    Value = strValue,
                                    Size = strValue.Length
                                };
                                command.Parameters.Add(param);
                            }
                            else
                            {
                                command.Parameters.AddWithValue($"@p{p}", value);
                            }
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
                catch (Exception batchEx)
                {
                    _logger.LogError("Batch import error for {TableName}: {Message}", tableName, batchEx.Message);
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
            return false;
        }
    }

    /// <summary>
    /// Checks if a table exists in the MariaDB database.
    /// </summary>
    public bool TableExists(string tableName)
    {
        if (_connection == null)
            throw new InvalidOperationException("Not connected to database");

        IdentifierValidator.ValidateOrThrow(tableName, "table name");

        try
        {
            var query = @"
                SELECT COUNT(*)
                FROM information_schema.tables
                WHERE table_schema = @database AND table_name = @tableName";

            using var command = new MySqlCommand(query, _connection);
            command.Parameters.AddWithValue("@database", _database);
            command.Parameters.AddWithValue("@tableName", tableName);

            var count = command.GetScalarValue<int>(0, _logger, $"table existence check for {tableName}");
            return count > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError("Error checking if table {TableName} exists: {Message}", tableName, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Gets the row count for a specific table.
    /// </summary>
    public int GetTableRowCount(string tableName)
    {
        if (_connection == null)
            throw new InvalidOperationException("Not connected to database");

        IdentifierValidator.ValidateOrThrow(tableName, "table name");

        try
        {
            var query = $"SELECT COUNT(*) FROM `{tableName}`";
            using var command = new MySqlCommand(query, _connection);

            return command.GetScalarValue<int>(0, _logger, $"row count for {tableName}");
        }
        catch (Exception ex)
        {
            _logger.LogError("Error getting row count for {TableName}: {Message}", tableName, ex.Message);
            return 0;
        }
    }

    /// <summary>
    /// Drops a table from the MariaDB database.
    /// </summary>
    public bool DropTable(string tableName)
    {
        if (_connection == null)
            throw new InvalidOperationException("Not connected to database");

        IdentifierValidator.ValidateOrThrow(tableName, "table name");

        try
        {
            var query = $"DROP TABLE IF EXISTS `{tableName}`";
            using var command = new MySqlCommand(query, _connection);
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
            _logger.LogInformation("Disabling foreign key constraints and unique checks for faster inserts");

            // Disable foreign key checks
            using (var command = new MySqlCommand("SET FOREIGN_KEY_CHECKS = 0", _connection))
            {
                command.ExecuteNonQuery();
            }

            // Disable unique constraint checks - this is a major performance boost
            using (var command = new MySqlCommand("SET unique_checks = 0", _connection))
            {
                command.ExecuteNonQuery();
            }

            // Try to set InnoDB auto-increment lock mode to fastest setting (requires SUPER privilege)
            // This is optional - if it fails, we'll just log a warning and continue
            try
            {
                using var command = new MySqlCommand("SET GLOBAL innodb_autoinc_lock_mode = 2", _connection);
                command.ExecuteNonQuery();
                _logger.LogInformation("Set innodb_autoinc_lock_mode to 2 for faster auto-increment handling");
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Could not set innodb_autoinc_lock_mode (requires SUPER privilege): {Message}", ex.Message);
            }

            _logger.LogInformation("Foreign key constraints and unique checks disabled");
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
            _logger.LogInformation("Re-enabling foreign key constraints and unique checks");

            // Re-enable foreign key checks
            using (var command = new MySqlCommand("SET FOREIGN_KEY_CHECKS = 1", _connection))
            {
                command.ExecuteNonQuery();
            }

            // Re-enable unique constraint checks
            using (var command = new MySqlCommand("SET unique_checks = 1", _connection))
            {
                command.ExecuteNonQuery();
            }

            _logger.LogInformation("Foreign key constraints and unique checks re-enabled");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError("Error re-enabling foreign key constraints: {Message}", ex.Message);
            return false;
        }
    }

    private string ConvertSqlServerTypeToMariaDB(string sqlServerType, bool isJsonColumn)
    {
        var baseType = sqlServerType.Replace(" NULL", "").Replace(" NOT NULL", "").Trim();
        var isNullable = !sqlServerType.Contains("NOT NULL");

        if (isJsonColumn)
            return "LONGTEXT" + (isNullable ? "" : " NOT NULL");

        var mariaType = baseType.ToUpper() switch
        {
            var t when t.StartsWith("VARCHAR") => t.Contains("MAX") ? "LONGTEXT" : t.Replace("VARCHAR", "VARCHAR"),
            var t when t.StartsWith("NVARCHAR") => "LONGTEXT",
            "INT" or "INTEGER" => "INT",
            "BIGINT" => "BIGINT",
            "SMALLINT" => "SMALLINT",
            "TINYINT" => "TINYINT",
            "BIT" => "BOOLEAN",
            var t when t.StartsWith("DECIMAL") => t.Replace("DECIMAL", "DECIMAL"),
            "FLOAT" => "DOUBLE",
            "REAL" => "FLOAT",
            "DATETIME" or "DATETIME2" or "SMALLDATETIME" => "DATETIME",
            "DATE" => "DATE",
            "TIME" => "TIME",
            "UNIQUEIDENTIFIER" => "CHAR(36)",
            var t when t.StartsWith("VARBINARY") => "LONGBLOB",
            "XML" => "LONGTEXT",
            _ => "LONGTEXT"
        };

        return mariaType + (isNullable ? "" : " NOT NULL");
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

                // Handle datetime with timezone
                if ((strValue.Contains('+') || strValue.EndsWith('Z')) &&
                    DateTimeHelper.IsLikelyIsoDateTime(strValue))
                {
                    return DateTimeHelper.RemoveTimezone(strValue) ?? strValue;
                }
            }

            return value;
        }).ToArray();
    }

    public bool SupportsBulkCopy()
    {
        return true; // MariaDB multi-row INSERT is optimized
    }

    /// <summary>
    /// Imports data using optimized multi-row INSERT statements for better performance.
    /// </summary>
    public bool ImportDataBulk(
        string tableName,
        List<string> columns,
        List<object[]> data)
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
            var actualColumns = GetTableColumns(tableName);
            if (actualColumns.Count == 0)
            {
                _logger.LogError("Could not retrieve columns for table {TableName}", tableName);
                return false;
            }

            var validColumnIndices = new List<int>();
            var validColumns = new List<string>();

            for (int i = 0; i < columns.Count; i++)
            {
                if (actualColumns.Contains(columns[i]))
                {
                    IdentifierValidator.ValidateOrThrow(columns[i], "column name");
                    validColumnIndices.Add(i);
                    validColumns.Add(columns[i]);
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

            _logger.LogInformation("Bulk importing {Count} rows into {TableName} using multi-row INSERT", filteredData.Count, tableName);

            const int rowsPerBatch = DbSeederConstants.LARGE_BATCH_SIZE;
            var totalImported = 0;

            for (int i = 0; i < filteredData.Count; i += rowsPerBatch)
            {
                var batch = filteredData.Skip(i).Take(rowsPerBatch).ToList();

                using var transaction = _connection.BeginTransaction();
                try
                {
                    // Build multi-row INSERT statement
                    var quotedColumns = validColumns.Select(col => $"`{col}`").ToList();
                    var columnPart = $"INSERT INTO `{tableName}` ({string.Join(", ", quotedColumns)}) VALUES ";

                    var valueSets = new List<string>();
                    var allParameters = new List<(string name, object value)>();
                    var paramIndex = 0;

                    foreach (var row in batch)
                    {
                        var preparedRow = PrepareRowForInsert(row, validColumns);
                        var rowParams = new List<string>();

                        for (int p = 0; p < preparedRow.Length; p++)
                        {
                            var paramName = $"@p{paramIndex}";
                            rowParams.Add(paramName);
                            allParameters.Add((paramName, preparedRow[p] ?? DBNull.Value));
                            paramIndex++;
                        }

                        valueSets.Add($"({string.Join(", ", rowParams)})");
                    }

                    var fullInsertSql = columnPart + string.Join(", ", valueSets);

                    using var command = new MySqlCommand(fullInsertSql, _connection, transaction);
                    command.CommandTimeout = DbSeederConstants.LARGE_BATCH_COMMAND_TIMEOUT;

                    // Add all parameters
                    foreach (var (name, value) in allParameters)
                    {
                        if (value is string strValue)
                        {
                            var param = new MySqlConnector.MySqlParameter
                            {
                                ParameterName = name,
                                MySqlDbType = MySqlConnector.MySqlDbType.LongText,
                                Value = strValue,
                                Size = strValue.Length
                            };
                            command.Parameters.Add(param);
                        }
                        else
                        {
                            command.Parameters.AddWithValue(name, value);
                        }
                    }

                    command.ExecuteNonQuery();
                    transaction.Commit();
                    totalImported += batch.Count;

                    if (filteredData.Count > DbSeederConstants.LOGGING_THRESHOLD)
                    {
                        _logger.LogDebug("Batch: {BatchCount} rows ({TotalImported}/{FilteredDataCount} total)", batch.Count, totalImported, filteredData.Count);
                    }
                }
                catch (Exception batchEx)
                {
                    _logger.LogError("Batch import error for {TableName}: {Message}", tableName, batchEx.Message);
                    transaction.SafeRollback(_connection, _logger, tableName);
                    throw;
                }
            }

            _logger.LogInformation("Successfully bulk imported {TotalImported} rows into {TableName}", totalImported, tableName);
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

    /// <summary>
    /// Tests the connection to MariaDB by executing a simple query.
    /// </summary>
    public bool TestConnection()
    {
        try
        {
            if (Connect())
            {
                using var command = new MySqlCommand("SELECT 1", _connection);
                var result = command.GetScalarValue<int>(0, _logger, "connection test");
                Disconnect();
                return result == 1;
            }
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError("MariaDB connection test failed: {Message}", ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Disposes of the MariaDB importer and releases all resources.
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
