using MySqlConnector;
using Bit.Seeder.Migration.Models;
using Bit.Seeder.Migration.Utils;
using Microsoft.Extensions.Logging;

namespace Bit.Seeder.Migration.Databases;

public class MariaDbImporter(DatabaseConfig config, ILogger<MariaDbImporter> logger) : IDatabaseImporter
{
    private readonly ILogger<MariaDbImporter> _logger = logger;
    private readonly string _host = config.Host;
    private readonly int _port = config.Port > 0 ? config.Port : 3306;
    private readonly string _database = config.Database;
    private readonly string _username = config.Username;
    private readonly string _password = config.Password;
    private MySqlConnection? _connection;

    public bool Connect()
    {
        try
        {
            var connectionString = $"Server={_host};Port={_port};Database={_database};" +
                                 $"Uid={_username};Pwd={_password};" +
                                 $"ConnectionTimeout=30;CharSet=utf8mb4;AllowLoadLocalInfile=true;MaxPoolSize=100;";

            _connection = new MySqlConnection(connectionString);
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
            var mariaColumns = new List<string>();
            foreach (var colName in columns)
            {
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

                    if (filteredData.Count > 1000)
                    {
                        _logger.LogDebug("Batch: {BatchCount} rows ({TotalImported}/{FilteredDataCount} total)", batch.Count, totalImported, filteredData.Count);
                    }
                }
                catch
                {
                    transaction.Rollback();
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

    public bool TableExists(string tableName)
    {
        if (_connection == null)
            throw new InvalidOperationException("Not connected to database");

        try
        {
            var query = @"
                SELECT COUNT(*)
                FROM information_schema.tables
                WHERE table_schema = @database AND table_name = @tableName";

            using var command = new MySqlCommand(query, _connection);
            command.Parameters.AddWithValue("@database", _database);
            command.Parameters.AddWithValue("@tableName", tableName);

            var count = Convert.ToInt32(command.ExecuteScalar());
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
            var query = $"SELECT COUNT(*) FROM `{tableName}`";
            using var command = new MySqlCommand(query, _connection);

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
            _logger.LogInformation("Disabling foreign key constraints");
            var query = "SET FOREIGN_KEY_CHECKS = 0";
            using var command = new MySqlCommand(query, _connection);
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
            var query = "SET FOREIGN_KEY_CHECKS = 1";
            using var command = new MySqlCommand(query, _connection);
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

    public bool TestConnection()
    {
        try
        {
            if (Connect())
            {
                using var command = new MySqlCommand("SELECT 1", _connection);
                var result = command.ExecuteScalar();
                Disconnect();
                return result != null && Convert.ToInt32(result) == 1;
            }
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError("MariaDB connection test failed: {Message}", ex.Message);
            return false;
        }
    }

    public void Dispose()
    {
        Disconnect();
    }
}
