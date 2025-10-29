using Microsoft.Data.Sqlite;
using Bit.Seeder.Migration.Models;
using Bit.Seeder.Migration.Utils;
using Microsoft.Extensions.Logging;

namespace Bit.Seeder.Migration.Databases;

public class SqliteImporter(DatabaseConfig config, ILogger<SqliteImporter> logger) : IDatabaseImporter
{
    private readonly ILogger<SqliteImporter> _logger = logger;
    private readonly string _databasePath = config.Database;
    private SqliteConnection? _connection;

    public bool Connect()
    {
        try
        {
            // Ensure directory exists
            var directory = Path.GetDirectoryName(_databasePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var connectionString = $"Data Source={_databasePath}";
            _connection = new SqliteConnection(connectionString);
            _connection.Open();

            // Enable foreign keys and set pragmas for better performance
            using (var command = new SqliteCommand("PRAGMA foreign_keys = ON", _connection))
            {
                command.ExecuteNonQuery();
            }
            using (var command = new SqliteCommand("PRAGMA journal_mode = WAL", _connection))
            {
                command.ExecuteNonQuery();
            }
            using (var command = new SqliteCommand("PRAGMA synchronous = NORMAL", _connection))
            {
                command.ExecuteNonQuery();
            }

            _logger.LogInformation("Connected to SQLite database: {DatabasePath}", _databasePath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to connect to SQLite: {Message}", ex.Message);
            return false;
        }
    }

    public void Disconnect()
    {
        if (_connection != null)
        {
            try
            {
                // Force completion of any pending WAL operations
                using (var command = new SqliteCommand("PRAGMA wal_checkpoint(TRUNCATE)", _connection))
                {
                    command.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Error during WAL checkpoint: {Message}", ex.Message);
            }

            _connection.Close();
            _connection.Dispose();
            _connection = null;
            _logger.LogInformation("Disconnected from SQLite");
        }
    }

    public bool CreateTableFromSchema(
        string tableName,
        List<string> columns,
        Dictionary<string, string> columnTypes,
        List<string>? specialColumns = null)
    {
        specialColumns ??= [];

        if (_connection == null)
            throw new InvalidOperationException("Not connected to database");

        try
        {
            var sqliteColumns = new List<string>();
            foreach (var colName in columns)
            {
                var sqlServerType = columnTypes.GetValueOrDefault(colName, "VARCHAR(MAX)");
                var sqliteType = ConvertSqlServerTypeToSQLite(sqlServerType, specialColumns.Contains(colName));
                sqliteColumns.Add($"\"{colName}\" {sqliteType}");
            }

            var createSql = $@"
                CREATE TABLE IF NOT EXISTS ""{tableName}"" (
                    {string.Join(",\n                    ", sqliteColumns)}
                )";

            _logger.LogInformation("Creating table {TableName} in SQLite", tableName);
            _logger.LogDebug("CREATE TABLE SQL: {CreateSql}", createSql);

            using var command = new SqliteCommand(createSql, _connection);
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
            var query = $"PRAGMA table_info(\"{tableName}\")";
            using var command = new SqliteCommand(query, _connection);
            using var reader = command.ExecuteReader();

            var columns = new List<string>();
            while (reader.Read())
            {
                columns.Add(reader.GetString(1)); // Column name is at index 1
            }

            return columns;
        }
        catch (Exception ex)
        {
            _logger.LogError("Error getting columns for table {TableName}: {Message}", tableName, ex.Message);
            return [];
        }
    }

    public bool ImportData(
        string tableName,
        List<string> columns,
        List<object[]> data,
        int batchSize = 1000)
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
            var actualColumns = GetTableColumns(tableName);
            if (actualColumns.Count == 0)
            {
                _logger.LogError("Could not retrieve columns for table {TableName}", tableName);
                return false;
            }

            // Filter columns
            var validColumnIndices = new List<int>();
            var validColumns = new List<string>();

            for (int i = 0; i < columns.Count; i++)
            {
                if (actualColumns.Contains(columns[i]))
                {
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
            var quotedColumns = validColumns.Select(col => $"\"{col}\"").ToList();
            var placeholders = string.Join(", ", Enumerable.Range(0, validColumns.Count).Select(i => $"@p{i}"));
            var insertSql = $"INSERT INTO \"{tableName}\" ({string.Join(", ", quotedColumns)}) VALUES ({placeholders})";

            // Begin transaction for all batches
            using var transaction = _connection.BeginTransaction();
            try
            {
                var totalImported = 0;
                for (int i = 0; i < filteredData.Count; i += batchSize)
                {
                    var batch = filteredData.Skip(i).Take(batchSize).ToList();

                    foreach (var row in batch)
                    {
                        using var command = new SqliteCommand(insertSql, _connection, transaction);

                        var preparedRow = PrepareRowForInsert(row, validColumns);
                        for (int p = 0; p < preparedRow.Length; p++)
                        {
                            var value = preparedRow[p] ?? DBNull.Value;

                            // For string values, explicitly set parameter type to avoid truncation
                            if (value is string strValue)
                            {
                                var param = command.Parameters.Add($"@p{p}", Microsoft.Data.Sqlite.SqliteType.Text);
                                param.Value = strValue;
                            }
                            else
                            {
                                command.Parameters.AddWithValue($"@p{p}", value);
                            }
                        }

                        command.ExecuteNonQuery();
                    }

                    totalImported += batch.Count;

                    if (filteredData.Count > 1000)
                    {
                        _logger.LogDebug("Batch: {BatchCount} rows ({TotalImported}/{FilteredDataCount} total)", batch.Count, totalImported, filteredData.Count);
                    }
                }

                transaction.Commit();

                _logger.LogInformation("Successfully imported {TotalImported} rows into {TableName}", totalImported, tableName);
                return true;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Error importing data into {TableName}: {Message}", tableName, ex.Message);
            return false;
        }
    }

    public bool TableExists(string tableName)
    {
        if (_connection == null)
            throw new InvalidOperationException("Not connected to database");

        try
        {
            var query = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name = @tableName";
            using var command = new SqliteCommand(query, _connection);
            command.Parameters.AddWithValue("@tableName", tableName);

            var count = Convert.ToInt64(command.ExecuteScalar());
            return count > 0;
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
            var query = $"SELECT COUNT(*) FROM \"{tableName}\"";
            using var command = new SqliteCommand(query, _connection);

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
            var query = $"DROP TABLE IF EXISTS \"{tableName}\"";
            using var command = new SqliteCommand(query, _connection);
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
            var query = "PRAGMA foreign_keys = OFF";
            using var command = new SqliteCommand(query, _connection);
            command.ExecuteNonQuery();

            _logger.LogInformation("Foreign key constraints disabled");
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
            var query = "PRAGMA foreign_keys = ON";
            using var command = new SqliteCommand(query, _connection);
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

    private string ConvertSqlServerTypeToSQLite(string sqlServerType, bool isJsonColumn)
    {
        var baseType = sqlServerType.Replace(" NULL", "").Replace(" NOT NULL", "").Trim().ToUpper();
        var isNullable = !sqlServerType.Contains("NOT NULL");

        if (isJsonColumn)
            return "TEXT" + (isNullable ? "" : " NOT NULL");

        // SQLite has only 5 storage classes: NULL, INTEGER, REAL, TEXT, BLOB
        string sqliteType;
        if (baseType.Contains("INT") || baseType.Contains("BIT"))
            sqliteType = "INTEGER";
        else if (baseType.Contains("DECIMAL") || baseType.Contains("NUMERIC") ||
                 baseType.Contains("FLOAT") || baseType.Contains("REAL") || baseType.Contains("MONEY"))
            sqliteType = "REAL";
        else if (baseType.Contains("BINARY") || baseType == "IMAGE")
            sqliteType = "BLOB";
        else
            sqliteType = "TEXT";

        return sqliteType + (isNullable ? "" : " NOT NULL");
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

                // Boolean to integer for SQLite
                if (strValue.Equals("true", StringComparison.OrdinalIgnoreCase))
                    return 1;
                if (strValue.Equals("false", StringComparison.OrdinalIgnoreCase))
                    return 0;

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

    public bool TestConnection()
    {
        try
        {
            if (Connect())
            {
                using var command = new SqliteCommand("SELECT 1", _connection);
                var result = command.ExecuteScalar();
                Disconnect();
                return result != null && Convert.ToInt32(result) == 1;
            }
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError("SQLite connection test failed: {Message}", ex.Message);
            return false;
        }
    }

    public void Dispose()
    {
        Disconnect();
    }
}
