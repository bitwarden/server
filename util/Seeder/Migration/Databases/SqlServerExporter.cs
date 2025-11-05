using Microsoft.Data.SqlClient;
using Bit.Seeder.Migration.Models;
using Bit.Seeder.Migration.Utils;
using Microsoft.Extensions.Logging;

namespace Bit.Seeder.Migration.Databases;

/// <summary>
/// SQL Server database exporter that handles schema discovery and data export.
/// </summary>
public class SqlServerExporter(DatabaseConfig config, ILogger<SqlServerExporter> logger) : IDisposable
{
    private readonly ILogger<SqlServerExporter> _logger = logger;
    private readonly string _host = config.Host;
    private readonly int _port = config.Port;
    private readonly string _database = config.Database;
    private readonly string _username = config.Username;
    private readonly string _password = config.Password;
    private SqlConnection? _connection;
    private bool _disposed = false;

    /// <summary>
    /// Connects to the SQL Server database.
    /// </summary>
    public bool Connect()
    {
        try
        {
            var safeConnectionString = $"Server={_host},{_port};Database={_database};" +
                                      $"User Id={_username};Password={DbSeederConstants.REDACTED_PASSWORD};" +
                                      $"TrustServerCertificate=True;" +
                                      $"Connection Timeout={DbSeederConstants.DEFAULT_CONNECTION_TIMEOUT};";

            var actualConnectionString = safeConnectionString.Replace(DbSeederConstants.REDACTED_PASSWORD, _password);

            _connection = new SqlConnection(actualConnectionString);
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

    /// <summary>
    /// Disconnects from the SQL Server database.
    /// </summary>
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

    /// <summary>
    /// Discovers all tables in the SQL Server database.
    /// </summary>
    /// <param name="excludeSystemTables">Whether to exclude system tables</param>
    /// <returns>List of table names</returns>
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
                var tableName = reader.GetString(0);

                // Validate table name immediately to prevent second-order SQL injection
                IdentifierValidator.ValidateOrThrow(tableName, "table name");

                tables.Add(tableName);
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

    /// <summary>
    /// Gets detailed information about a table including columns, types, and row count.
    /// </summary>
    /// <param name="tableName">The name of the table to query</param>
    /// <returns>TableInfo containing schema and metadata</returns>
    public TableInfo GetTableInfo(string tableName)
    {
        if (_connection == null)
            throw new InvalidOperationException("Not connected to database");

        IdentifierValidator.ValidateOrThrow(tableName, "table name");

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

                    // Validate column name immediately to prevent second-order SQL injection
                    IdentifierValidator.ValidateOrThrow(colName, "column name");

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

            var countQuery = $"SELECT COUNT(*) FROM [{tableName}]";
            int rowCount;

            using (var command = new SqlCommand(countQuery, _connection))
            {
                rowCount = command.GetScalarValue<int>(0, _logger, $"row count for {tableName}");
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

    /// <summary>
    /// Exports all data from a table with streaming to avoid memory exhaustion.
    /// </summary>
    /// <param name="tableName">The name of the table to export</param>
    /// <param name="batchSize">Batch size for progress reporting</param>
    /// <returns>Tuple of column names and data rows</returns>
    public (List<string> Columns, List<object[]> Data) ExportTableData(
        string tableName,
        int batchSize = DbSeederConstants.PROGRESS_REPORTING_INTERVAL)
    {
        if (_connection == null)
            throw new InvalidOperationException("Not connected to database");

        IdentifierValidator.ValidateOrThrow(tableName, "table name");

        try
        {
            // Get table info first
            var tableInfo = GetTableInfo(tableName);

            // Validate all column names
            foreach (var colName in tableInfo.Columns)
            {
                IdentifierValidator.ValidateOrThrow(colName, "column name");
            }

            // Build column list with proper quoting
            var quotedColumns = tableInfo.Columns.Select(col => $"[{col}]").ToList();
            var columnList = string.Join(", ", quotedColumns);

            // Execute query
            var query = $"SELECT {columnList} FROM [{tableName}]";
            _logger.LogInformation("Executing export query for {TableName}", tableName);

            using var command = new SqlCommand(query, _connection);
            command.CommandTimeout = DbSeederConstants.LARGE_BATCH_COMMAND_TIMEOUT;

            using var reader = command.ExecuteReader();

            // Identify GUID columns for in-place uppercase conversion
            var guidColumnIndices = IdentifyGuidColumns(tableInfo);

            // Fetch data in batches - still loads into memory but with progress reporting
            // Note: For true streaming, consumers should use yield return pattern
            var allData = new List<object[]>();
            while (reader.Read())
            {
                var row = new object[tableInfo.Columns.Count];
                reader.GetValues(row);

                // Convert GUID values in-place to uppercase for Bitwarden compatibility
                ConvertGuidsToUppercaseInPlace(row, guidColumnIndices);

                allData.Add(row);

                if (allData.Count % batchSize == 0)
                {
                    _logger.LogDebug("Fetched {Count} rows from {TableName}", allData.Count, tableName);
                }
            }

            _logger.LogInformation("Exported {Count} rows from {TableName}", allData.Count, tableName);
            return (tableInfo.Columns, allData);
        }
        catch (Exception ex)
        {
            _logger.LogError("Error exporting data from {TableName}: {Message}", tableName, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Identifies columns that likely contain JSON data by sampling values.
    /// </summary>
    /// <param name="tableName">The name of the table to analyze</param>
    /// <param name="sampleSize">Number of rows to sample for analysis</param>
    /// <returns>List of column names that appear to contain JSON</returns>
    public List<string> IdentifyJsonColumns(
        string tableName,
        int sampleSize = DbSeederConstants.DEFAULT_SAMPLE_SIZE)
    {
        if (_connection == null)
            throw new InvalidOperationException("Not connected to database");

        IdentifierValidator.ValidateOrThrow(tableName, "table name");

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

            // Validate all column names
            foreach (var colName in textColumns)
            {
                IdentifierValidator.ValidateOrThrow(colName, "column name");
            }

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
            // JSON detection threshold: 50% of samples must look like JSON
            const double jsonThreshold = 0.5;

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

                // If more than threshold of non-null values look like JSON, mark as JSON column
                if (totalNonNull > 0 && (double)jsonIndicators / totalNonNull > jsonThreshold)
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

    /// <summary>
    /// Identifies GUID column indices in a table for efficient processing.
    /// </summary>
    /// <param name="tableInfo">Table metadata including column types</param>
    /// <returns>List of column indices that are GUID/uniqueidentifier columns</returns>
    private List<int> IdentifyGuidColumns(TableInfo tableInfo)
    {
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

        if (guidColumnIndices.Count > 0)
        {
            _logger.LogInformation("Converting {Count} GUID column(s) to uppercase", guidColumnIndices.Count);
        }

        return guidColumnIndices;
    }

    /// <summary>
    /// Converts GUID values to uppercase in-place within a data row.
    /// More efficient than creating new arrays as it modifies the array directly.
    /// </summary>
    /// <param name="row">The data row to modify</param>
    /// <param name="guidColumnIndices">Indices of columns containing GUIDs</param>
    private void ConvertGuidsToUppercaseInPlace(object[] row, List<int> guidColumnIndices)
    {
        foreach (var guidIdx in guidColumnIndices)
        {
            if (guidIdx < row.Length && row[guidIdx] != null && row[guidIdx] != DBNull.Value)
            {
                var guidValue = row[guidIdx].ToString();
                // Convert to uppercase in-place, preserving the GUID format
                row[guidIdx] = guidValue?.ToUpper() ?? string.Empty;
            }
        }
    }

    /// <summary>
    /// Tests the connection to SQL Server by executing a simple query.
    /// </summary>
    public bool TestConnection()
    {
        try
        {
            if (Connect())
            {
                using var command = new SqlCommand("SELECT 1", _connection);
                // Use null-safe scalar value retrieval
                var result = command.GetScalarValue<int>(0, _logger, "connection test");
                Disconnect();
                return result == 1;
            }
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError("Connection test failed: {Message}", ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Disposes of the SQL Server exporter and releases all resources.
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
