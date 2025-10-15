using Microsoft.Data.SqlClient;
using Bit.Seeder.Migration.Models;
using Microsoft.Extensions.Logging;

namespace Bit.Seeder.Migration.Databases;

public class SqlServerExporter(DatabaseConfig config, ILogger<SqlServerExporter> logger) : IDisposable
{
    private readonly ILogger<SqlServerExporter> _logger = logger;
    private readonly string _host = config.Host;
    private readonly int _port = config.Port;
    private readonly string _database = config.Database;
    private readonly string _username = config.Username;
    private readonly string _password = config.Password;
    private SqlConnection? _connection;

    public bool Connect()
    {
        try
        {
            var connectionString = $"Server={_host},{_port};Database={_database};" +
                                 $"User Id={_username};Password={_password};" +
                                 $"TrustServerCertificate=True;Connection Timeout=30;";

            _connection = new SqlConnection(connectionString);
            _connection.Open();

            _logger.LogInformation("Connected to SQL Server: {Host}/{Database}", _host, _database);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to connect to SQL Server: {Message}", ex.Message);
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
            _logger.LogInformation("Disconnected from SQL Server");
        }
    }

    public List<string> DiscoverTables(bool excludeSystemTables = true)
    {
        if (_connection == null)
            throw new InvalidOperationException("Not connected to database");

        try
        {
            var query = @"
                SELECT TABLE_NAME
                FROM INFORMATION_SCHEMA.TABLES
                WHERE TABLE_TYPE = 'BASE TABLE'";

            if (excludeSystemTables)
            {
                query += @"
                    AND TABLE_SCHEMA = 'dbo'
                    AND TABLE_NAME NOT IN ('sysdiagrams', '__EFMigrationsHistory')";
            }

            query += " ORDER BY TABLE_NAME";

            using var command = new SqlCommand(query, _connection);
            using var reader = command.ExecuteReader();

            var tables = new List<string>();
            while (reader.Read())
            {
                tables.Add(reader.GetString(0));
            }

            _logger.LogInformation("Discovered {Count} tables: {Tables}", tables.Count, string.Join(", ", tables));
            return tables;
        }
        catch (Exception ex)
        {
            _logger.LogError("Error discovering tables: {Message}", ex.Message);
            throw;
        }
    }

    public TableInfo GetTableInfo(string tableName)
    {
        if (_connection == null)
            throw new InvalidOperationException("Not connected to database");

        try
        {
            // Get column information
            var columnQuery = @"
                SELECT
                    COLUMN_NAME,
                    DATA_TYPE,
                    IS_NULLABLE,
                    CHARACTER_MAXIMUM_LENGTH,
                    NUMERIC_PRECISION,
                    NUMERIC_SCALE
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_NAME = @TableName
                ORDER BY ORDINAL_POSITION";

            var columns = new List<string>();
            var columnTypes = new Dictionary<string, string>();

            using (var command = new SqlCommand(columnQuery, _connection))
            {
                command.Parameters.AddWithValue("@TableName", tableName);
                using var reader = command.ExecuteReader();

                while (reader.Read())
                {
                    var colName = reader.GetString(0);
                    var dataType = reader.GetString(1);
                    var isNullable = reader.GetString(2);
                    var maxLength = reader.IsDBNull(3) ? (int?)null : reader.GetInt32(3);
                    var precision = reader.IsDBNull(4) ? (byte?)null : reader.GetByte(4);
                    var scale = reader.IsDBNull(5) ? (int?)null : reader.GetInt32(5);

                    columns.Add(colName);

                    // Build type description
                    var typeDesc = dataType.ToUpper();
                    if (maxLength.HasValue && dataType.ToLower() is "varchar" or "nvarchar" or "char" or "nchar")
                    {
                        typeDesc += $"({maxLength})";
                    }
                    else if (precision.HasValue && dataType.ToLower() is "decimal" or "numeric")
                    {
                        typeDesc += $"({precision},{scale})";
                    }

                    typeDesc += isNullable == "YES" ? " NULL" : " NOT NULL";
                    columnTypes[colName] = typeDesc;
                }
            }

            if (columns.Count == 0)
            {
                throw new InvalidOperationException($"Table '{tableName}' not found");
            }

            // Get row count
            var countQuery = $"SELECT COUNT(*) FROM [{tableName}]";
            int rowCount;

            using (var command = new SqlCommand(countQuery, _connection))
            {
                rowCount = (int)command.ExecuteScalar()!;
            }

            _logger.LogInformation("Table {TableName}: {ColumnCount} columns, {RowCount} rows", tableName, columns.Count, rowCount);

            return new TableInfo
            {
                Name = tableName,
                Columns = columns,
                ColumnTypes = columnTypes,
                RowCount = rowCount
            };
        }
        catch (Exception ex)
        {
            _logger.LogError("Error getting table info for {TableName}: {Message}", tableName, ex.Message);
            throw;
        }
    }

    public (List<string> Columns, List<object[]> Data) ExportTableData(string tableName, int batchSize = 10000)
    {
        if (_connection == null)
            throw new InvalidOperationException("Not connected to database");

        try
        {
            // Get table info first
            var tableInfo = GetTableInfo(tableName);

            // Build column list with proper quoting
            var quotedColumns = tableInfo.Columns.Select(col => $"[{col}]").ToList();
            var columnList = string.Join(", ", quotedColumns);

            // Execute query
            var query = $"SELECT {columnList} FROM [{tableName}]";
            _logger.LogInformation("Executing export query for {TableName}", tableName);

            using var command = new SqlCommand(query, _connection);
            command.CommandTimeout = 300; // 5 minutes

            using var reader = command.ExecuteReader();

            // Fetch data in batches
            var allData = new List<object[]>();
            while (reader.Read())
            {
                var row = new object[tableInfo.Columns.Count];
                reader.GetValues(row);
                allData.Add(row);

                if (allData.Count % batchSize == 0)
                {
                    _logger.LogDebug("Fetched {Count} rows from {TableName}", allData.Count, tableName);
                }
            }

            // Convert GUID values to uppercase to ensure compatibility with Bitwarden
            var processedData = ConvertGuidsToUppercase(allData, tableInfo);

            _logger.LogInformation("Exported {Count} rows from {TableName}", processedData.Count, tableName);
            return (tableInfo.Columns, processedData);
        }
        catch (Exception ex)
        {
            _logger.LogError("Error exporting data from {TableName}: {Message}", tableName, ex.Message);
            throw;
        }
    }

    public List<string> IdentifyJsonColumns(string tableName, int sampleSize = 100)
    {
        if (_connection == null)
            throw new InvalidOperationException("Not connected to database");

        try
        {
            var tableInfo = GetTableInfo(tableName);
            var jsonColumns = new List<string>();

            // Only check varchar/text columns
            var textColumns = tableInfo.ColumnTypes
                .Where(kv => kv.Value.ToLower().Contains("varchar") ||
                           kv.Value.ToLower().Contains("text") ||
                           kv.Value.ToLower().Contains("nvarchar"))
                .Select(kv => kv.Key)
                .ToList();

            if (textColumns.Count == 0)
                return jsonColumns;

            // Sample data from text columns
            var quotedColumns = textColumns.Select(col => $"[{col}]").ToList();
            var columnList = string.Join(", ", quotedColumns);

            var whereClause = string.Join(" OR ", textColumns.Select(col => $"[{col}] IS NOT NULL"));

            var query = $@"
                SELECT TOP {sampleSize} {columnList}
                FROM [{tableName}]
                WHERE {whereClause}";

            using var command = new SqlCommand(query, _connection);
            using var reader = command.ExecuteReader();

            var sampleData = new List<object[]>();
            while (reader.Read())
            {
                var row = new object[textColumns.Count];
                reader.GetValues(row);
                sampleData.Add(row);
            }

            // Analyze each column
            for (int i = 0; i < textColumns.Count; i++)
            {
                var colName = textColumns[i];
                var jsonIndicators = 0;
                var totalNonNull = 0;

                foreach (var row in sampleData)
                {
                    if (i < row.Length && row[i] != DBNull.Value)
                    {
                        totalNonNull++;
                        var value = row[i]?.ToString()?.Trim() ?? string.Empty;

                        // Check for JSON indicators
                        if ((value.StartsWith("{") && value.EndsWith("}")) ||
                            (value.StartsWith("[") && value.EndsWith("]")))
                        {
                            jsonIndicators++;
                        }
                    }
                }

                // If more than 50% of non-null values look like JSON, mark as JSON column
                if (totalNonNull > 0 && (double)jsonIndicators / totalNonNull > 0.5)
                {
                    jsonColumns.Add(colName);
                    _logger.LogInformation("Identified {ColumnName} as likely JSON column ({JsonIndicators}/{TotalNonNull} samples)", colName, jsonIndicators, totalNonNull);
                }
            }

            return jsonColumns;
        }
        catch (Exception ex)
        {
            _logger.LogError("Error identifying JSON columns in {TableName}: {Message}", tableName, ex.Message);
            return [];
        }
    }

    private List<object[]> ConvertGuidsToUppercase(List<object[]> data, TableInfo tableInfo)
    {
        if (data.Count == 0 || tableInfo.ColumnTypes.Count == 0)
            return data;

        // Identify GUID columns (uniqueidentifier type in SQL Server)
        var guidColumnIndices = new List<int>();
        for (int i = 0; i < tableInfo.Columns.Count; i++)
        {
            var columnName = tableInfo.Columns[i];
            if (tableInfo.ColumnTypes.TryGetValue(columnName, out var columnType))
            {
                if (columnType.ToUpper().Contains("UNIQUEIDENTIFIER"))
                {
                    guidColumnIndices.Add(i);
                    _logger.LogDebug("Found GUID column '{ColumnName}' at index {Index}", columnName, i);
                }
            }
        }

        if (guidColumnIndices.Count == 0)
        {
            _logger.LogDebug("No GUID columns found, returning data unchanged");
            return data;
        }

        _logger.LogInformation("Converting {Count} GUID column(s) to uppercase", guidColumnIndices.Count);

        // Process each row and convert GUID values to uppercase
        var processedData = new List<object[]>();
        foreach (var row in data)
        {
            var rowList = row.ToList();
            foreach (var guidIdx in guidColumnIndices)
            {
                if (guidIdx < rowList.Count && rowList[guidIdx] != null && rowList[guidIdx] != DBNull.Value)
                {
                    var guidValue = rowList[guidIdx].ToString();
                    // Convert to uppercase, preserving the GUID format
                    rowList[guidIdx] = guidValue?.ToUpper() ?? string.Empty;
                }
            }
            processedData.Add(rowList.ToArray());
        }

        return processedData;
    }

    public bool TestConnection()
    {
        try
        {
            if (Connect())
            {
                using var command = new SqlCommand("SELECT 1", _connection);
                var result = command.ExecuteScalar();
                Disconnect();
                return result != null && (int)result == 1;
            }
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError("Connection test failed: {Message}", ex.Message);
            return false;
        }
    }

    public void Dispose()
    {
        Disconnect();
    }
}
