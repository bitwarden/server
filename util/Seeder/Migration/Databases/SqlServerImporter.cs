using Microsoft.Data.SqlClient;
using Bit.Seeder.Migration.Models;
using Bit.Seeder.Migration.Utils;
using Microsoft.Extensions.Logging;
using System.Data;

namespace Bit.Seeder.Migration.Databases;

public class SqlServerImporter(DatabaseConfig config, ILogger<SqlServerImporter> logger) : IDatabaseImporter
{
    private readonly ILogger<SqlServerImporter> _logger = logger;
    private readonly string _host = config.Host;
    private readonly int _port = config.Port;
    private readonly string _database = config.Database;
    private readonly string _username = config.Username;
    private readonly string _password = config.Password;
    private SqlConnection? _connection;
    private const string _trackingTableName = "[dbo].[_MigrationDisabledConstraint]";

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

    public List<string> GetTableColumns(string tableName)
    {
        if (_connection == null)
            throw new InvalidOperationException("Not connected to database");

        try
        {
            var query = @"
                SELECT COLUMN_NAME
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_NAME = @TableName
                ORDER BY ORDINAL_POSITION";

            using var command = new SqlCommand(query, _connection);
            command.Parameters.AddWithValue("@TableName", tableName);

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
            var query = @"
                SELECT COLUMN_NAME, DATA_TYPE
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_NAME = @TableName";

            using var command = new SqlCommand(query, _connection);
            command.Parameters.AddWithValue("@TableName", tableName);

            var columnTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
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

    public bool TableExists(string tableName)
    {
        if (_connection == null)
            throw new InvalidOperationException("Not connected to database");

        try
        {
            var query = @"
                SELECT COUNT(*)
                FROM INFORMATION_SCHEMA.TABLES
                WHERE TABLE_NAME = @TableName AND TABLE_TYPE = 'BASE TABLE'";

            using var command = new SqlCommand(query, _connection);
            command.Parameters.AddWithValue("@TableName", tableName);

            var count = (int)command.ExecuteScalar()!;
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
            var query = $"SELECT COUNT(*) FROM [{tableName}]";
            using var command = new SqlCommand(query, _connection);

            var count = (int)command.ExecuteScalar()!;
            _logger.LogDebug("Row count for {TableName}: {Count}", tableName, count);
            return count;
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
            var query = $"DROP TABLE IF EXISTS [{tableName}]";
            using var command = new SqlCommand(query, _connection);
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

    private bool DropTrackingTable()
    {
        if (_connection == null)
            throw new InvalidOperationException("Not connected to database");

        try
        {
            var dropSql = $"DROP TABLE IF EXISTS {_trackingTableName}";
            using var command = new SqlCommand(dropSql, _connection);
            command.ExecuteNonQuery();

            _logger.LogDebug("Dropped tracking table {TrackingTableName}", _trackingTableName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Error dropping tracking table: {Message}", ex.Message);
            return false;
        }
    }

    private List<(string Schema, string Table, string Constraint)> GetConstraintsToReEnable()
    {
        if (_connection == null)
            throw new InvalidOperationException("Not connected to database");

        var constraints = new List<(string Schema, string Table, string Constraint)>();

        try
        {
            // Check if tracking table exists
            var checkSql = "SELECT COUNT(*) FROM sys.tables WHERE name = '_MigrationDisabledConstraint' AND schema_id = SCHEMA_ID('dbo')";
            using var checkCommand = new SqlCommand(checkSql, _connection);
            var tableExists = (int)checkCommand.ExecuteScalar()! > 0;

            if (!tableExists)
            {
                _logger.LogDebug("Tracking table does not exist, no constraints to re-enable");
                return constraints;
            }

            // Get only constraints that we disabled (PreExistingDisabled = 0)
            var querySql = $@"
                SELECT SchemaName, TableName, ConstraintName
                FROM {_trackingTableName}
                WHERE PreExistingDisabled = 0";

            using var command = new SqlCommand(querySql, _connection);
            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                constraints.Add((
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2)
                ));
            }

            _logger.LogDebug("Found {Count} constraints to re-enable from tracking table", constraints.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Error reading tracking table: {Message}", ex.Message);
        }

        return constraints;
    }

    public bool DisableForeignKeys()
    {
        if (_connection == null)
            throw new InvalidOperationException("Not connected to database");

        try
        {
            _logger.LogInformation("Disabling foreign key constraints for SQL Server");

            // Check if tracking table already exists
            var checkSql = "SELECT COUNT(*) FROM sys.tables WHERE name = '_MigrationDisabledConstraint' AND schema_id = SCHEMA_ID('dbo')";
            using (var checkCommand = new SqlCommand(checkSql, _connection))
            {
                var tableExists = (int)checkCommand.ExecuteScalar()! > 0;

                if (tableExists)
                {
                    // Table exists - this means we're resuming from an interrupted run
                    // Constraints are already disabled and tracked
                    _logger.LogInformation("Tracking table already exists - resuming from previous interrupted run");
                    _logger.LogInformation("Foreign key constraints are already disabled");
                    return true;
                }
            }

            // Table doesn't exist - this is a fresh run
            // Create table and disable constraints in a transaction for atomicity
            using var transaction = _connection.BeginTransaction();
            try
            {
                // Create tracking table
                var createSql = $@"
                    CREATE TABLE {_trackingTableName} (
                        SchemaName NVARCHAR(128) NOT NULL,
                        TableName NVARCHAR(128) NOT NULL,
                        ConstraintName NVARCHAR(128) NOT NULL,
                        PreExistingDisabled BIT NOT NULL,
                        DisabledAt DATETIME2 NOT NULL DEFAULT GETDATE()
                    )";

                using (var createCommand = new SqlCommand(createSql, _connection, transaction))
                {
                    createCommand.ExecuteNonQuery();
                }

                _logger.LogDebug("Created tracking table {TrackingTableName}", _trackingTableName);

                // First, get all PRE-EXISTING disabled foreign key constraints
                var preExistingQuery = @"
                    SELECT
                        OBJECT_SCHEMA_NAME(parent_object_id) AS schema_name,
                        OBJECT_NAME(parent_object_id) AS table_name,
                        name AS constraint_name
                    FROM sys.foreign_keys
                    WHERE is_disabled = 1";

                var preExistingConstraints = new List<(string Schema, string Table, string Constraint)>();
                using (var preCommand = new SqlCommand(preExistingQuery, _connection, transaction))
                using (var preReader = preCommand.ExecuteReader())
                {
                    while (preReader.Read())
                    {
                        preExistingConstraints.Add((
                            preReader.GetString(0),
                            preReader.GetString(1),
                            preReader.GetString(2)
                        ));
                    }
                }

                // Store pre-existing disabled constraints
                foreach (var (schema, table, constraint) in preExistingConstraints)
                {
                    var insertSql = $@"
                        INSERT INTO {_trackingTableName} (SchemaName, TableName, ConstraintName, PreExistingDisabled)
                        VALUES (@Schema, @Table, @Constraint, 1)";

                    using var insertCommand = new SqlCommand(insertSql, _connection, transaction);
                    insertCommand.Parameters.AddWithValue("@Schema", schema);
                    insertCommand.Parameters.AddWithValue("@Table", table);
                    insertCommand.Parameters.AddWithValue("@Constraint", constraint);
                    insertCommand.ExecuteNonQuery();
                }

                if (preExistingConstraints.Count > 0)
                {
                    _logger.LogInformation("Found {Count} pre-existing disabled constraints", preExistingConstraints.Count);
                }

                // Now get all ENABLED foreign key constraints
                var enabledQuery = @"
                    SELECT
                        OBJECT_SCHEMA_NAME(parent_object_id) AS schema_name,
                        OBJECT_NAME(parent_object_id) AS table_name,
                        name AS constraint_name
                    FROM sys.foreign_keys
                    WHERE is_disabled = 0";

                var constraints = new List<(string Schema, string Table, string Constraint)>();
                using (var command = new SqlCommand(enabledQuery, _connection, transaction))
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        constraints.Add((
                            reader.GetString(0),
                            reader.GetString(1),
                            reader.GetString(2)
                        ));
                    }
                }

                // Disable each enabled constraint and track it
                var disabledCount = 0;
                foreach (var (schema, table, constraint) in constraints)
                {
                    try
                    {
                        // Disable the constraint
                        var disableSql = $"ALTER TABLE [{schema}].[{table}] NOCHECK CONSTRAINT [{constraint}]";
                        using var disableCommand = new SqlCommand(disableSql, _connection, transaction);
                        disableCommand.ExecuteNonQuery();

                        // Store in tracking table with PreExistingDisabled = false
                        var insertSql = $@"
                            INSERT INTO {_trackingTableName} (SchemaName, TableName, ConstraintName, PreExistingDisabled)
                            VALUES (@Schema, @Table, @Constraint, 0)";

                        using var insertCommand = new SqlCommand(insertSql, _connection, transaction);
                        insertCommand.Parameters.AddWithValue("@Schema", schema);
                        insertCommand.Parameters.AddWithValue("@Table", table);
                        insertCommand.Parameters.AddWithValue("@Constraint", constraint);
                        insertCommand.ExecuteNonQuery();

                        disabledCount++;
                        _logger.LogDebug("Disabled constraint: {Constraint} on {Schema}.{Table}", constraint, schema, table);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("Could not disable constraint {Constraint}: {Message}", constraint, ex.Message);
                    }
                }

                // Commit the transaction - this makes everything atomic
                transaction.Commit();

                _logger.LogInformation("Disabled {Count} foreign key constraints", disabledCount);
                return true;
            }
            catch
            {
                // If anything fails, rollback the transaction
                // This ensures the tracking table doesn't exist with incomplete data
                transaction.Rollback();
                throw;
            }
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
            _logger.LogInformation("Re-enabling foreign key constraints for SQL Server");

            // Get constraints that we disabled (PreExistingDisabled = 0) from tracking table
            var constraintsToReEnable = GetConstraintsToReEnable();

            if (constraintsToReEnable.Count == 0)
            {
                _logger.LogInformation("No constraints to re-enable");
                // Still drop tracking table to clean up
                DropTrackingTable();
                return true;
            }

            var enabledCount = 0;
            foreach (var (schema, table, constraint) in constraintsToReEnable)
            {
                try
                {
                    var enableSql = $"ALTER TABLE [{schema}].[{table}] CHECK CONSTRAINT [{constraint}]";
                    using var command = new SqlCommand(enableSql, _connection);
                    command.ExecuteNonQuery();

                    enabledCount++;
                    _logger.LogDebug("Re-enabled constraint: {Constraint} on {Schema}.{Table}", constraint, schema, table);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Could not re-enable constraint {Constraint}: {Message}", constraint, ex.Message);
                }
            }

            _logger.LogInformation("Re-enabled {EnabledCount}/{TotalCount} foreign key constraints", enabledCount, constraintsToReEnable.Count);

            // Drop tracking table to clean up
            DropTrackingTable();

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError("Error re-enabling foreign key constraints: {Message}", ex.Message);
            return false;
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
            // Build column definitions
            var sqlServerColumns = new List<string>();
            foreach (var colName in columns)
            {
                var colType = columnTypes.GetValueOrDefault(colName, "NVARCHAR(MAX)");

                // If it's a special JSON column, ensure it's a large text type
                if (specialColumns.Contains(colName) &&
                    !colType.ToUpper().Contains("VARCHAR(MAX)") &&
                    !colType.ToUpper().Contains("TEXT"))
                {
                    colType = "NVARCHAR(MAX)";
                }

                sqlServerColumns.Add($"[{colName}] {colType}");
            }

            // Build CREATE TABLE statement
            var createSql = $@"
                CREATE TABLE [{tableName}] (
                    {string.Join(",\n                    ", sqlServerColumns)}
                )";

            _logger.LogInformation("Creating table {TableName} in SQL Server", tableName);
            _logger.LogDebug("CREATE TABLE SQL: {CreateSql}", createSql);

            using var command = new SqlCommand(createSql, _connection);
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

    public List<string> GetIdentityColumns(string tableName)
    {
        if (_connection == null)
            throw new InvalidOperationException("Not connected to database");

        try
        {
            var query = @"
                SELECT COLUMN_NAME
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_NAME = @TableName
                AND COLUMNPROPERTY(OBJECT_ID(TABLE_SCHEMA + '.' + TABLE_NAME), COLUMN_NAME, 'IsIdentity') = 1";

            using var command = new SqlCommand(query, _connection);
            command.Parameters.AddWithValue("@TableName", tableName);

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
            _logger.LogError("Error getting identity columns for table {TableName}: {Message}", tableName, ex.Message);
            return [];
        }
    }

    public bool EnableIdentityInsert(string tableName)
    {
        if (_connection == null)
            throw new InvalidOperationException("Not connected to database");

        try
        {
            var query = $"SET IDENTITY_INSERT [{tableName}] ON";
            using var command = new SqlCommand(query, _connection);
            command.ExecuteNonQuery();

            _logger.LogDebug("Enabled IDENTITY_INSERT for {TableName}", tableName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError("Error enabling IDENTITY_INSERT for {TableName}: {Message}", tableName, ex.Message);
            return false;
        }
    }

    public bool DisableIdentityInsert(string tableName)
    {
        if (_connection == null)
            throw new InvalidOperationException("Not connected to database");

        try
        {
            var query = $"SET IDENTITY_INSERT [{tableName}] OFF";
            using var command = new SqlCommand(query, _connection);
            command.ExecuteNonQuery();

            _logger.LogDebug("Disabled IDENTITY_INSERT for {TableName}", tableName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError("Error disabling IDENTITY_INSERT for {TableName}: {Message}", tableName, ex.Message);
            return false;
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
            // Get actual table columns from SQL Server
            var actualColumns = GetTableColumns(tableName);
            if (actualColumns.Count == 0)
            {
                _logger.LogError("Could not retrieve columns for table {TableName}", tableName);
                return false;
            }

            // Filter columns and data
            var validColumnIndices = new List<int>();
            var validColumns = new List<string>();
            var missingColumns = new List<string>();

            for (int i = 0; i < columns.Count; i++)
            {
                if (actualColumns.Contains(columns[i]))
                {
                    validColumnIndices.Add(i);
                    validColumns.Add(columns[i]);
                }
                else
                {
                    missingColumns.Add(columns[i]);
                }
            }

            if (missingColumns.Count > 0)
            {
                _logger.LogWarning("Skipping columns that don't exist in {TableName}: {Columns}", tableName, string.Join(", ", missingColumns));
            }

            if (validColumns.Count == 0)
            {
                _logger.LogError("No valid columns found for table {TableName}", tableName);
                return false;
            }

            // Filter data to only include valid columns
            var filteredData = data.Select(row =>
                validColumnIndices.Select(i => i < row.Length ? row[i] : null).ToArray()
            ).ToList();

            _logger.LogInformation("Valid columns for {TableName}: {Columns}", tableName, string.Join(", ", validColumns));

            // Check if table has identity columns
            var identityColumns = GetIdentityColumns(tableName);
            var identityColumnsInData = validColumns.Intersect(identityColumns).ToList();
            var needsIdentityInsert = identityColumnsInData.Count > 0;

            if (needsIdentityInsert)
            {
                _logger.LogInformation("Table {TableName} has identity columns in import data: {Columns}", tableName, string.Join(", ", identityColumnsInData));
                _logger.LogInformation("Enabling IDENTITY_INSERT to allow explicit identity values");

                if (!EnableIdentityInsert(tableName))
                {
                    _logger.LogError("Could not enable IDENTITY_INSERT for {TableName}", tableName);
                    return false;
                }
            }

            // Check for existing data
            var existingCount = GetTableRowCount(tableName);
            if (existingCount > 0)
            {
                _logger.LogWarning("Table {TableName} already contains {ExistingCount} rows - potential for primary key conflicts", tableName, existingCount);
            }

            // Import using batch insert
            var totalImported = FastBatchImport(tableName, validColumns, filteredData, batchSize);

            _logger.LogInformation("Successfully imported {TotalImported} rows into {TableName}", totalImported, tableName);

            // Disable IDENTITY_INSERT if it was enabled
            if (needsIdentityInsert)
            {
                if (!DisableIdentityInsert(tableName))
                {
                    _logger.LogWarning("Could not disable IDENTITY_INSERT for {TableName}", tableName);
                }
            }

            // Validate that data was actually inserted
            var actualCount = GetTableRowCount(tableName);
            _logger.LogInformation("Post-import validation for {TableName}: imported {TotalImported}, table contains {ActualCount}", tableName, totalImported, actualCount);

            if (actualCount < totalImported)
            {
                _logger.LogError("Import validation failed for {TableName}: expected at least {Expected}, found {Actual}", tableName, totalImported, actualCount);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError("Error importing data into {TableName}: {Message}", tableName, ex.Message);
            return false;
        }
    }

    private int UseSqlBulkCopy(string tableName, List<string> columns, List<object?[]> data)
    {
        try
        {
            using var bulkCopy = new SqlBulkCopy(_connection!)
            {
                DestinationTableName = $"[{tableName}]",
                BatchSize = 10000,
                BulkCopyTimeout = 600 // 10 minutes
            };

            // Map columns
            foreach (var column in columns)
            {
                bulkCopy.ColumnMappings.Add(column, column);
            }

            // Create DataTable
            var dataTable = new DataTable();
            foreach (var column in columns)
            {
                dataTable.Columns.Add(column, typeof(object));
            }

            // Add rows with data type conversion
            foreach (var row in data)
            {
                var preparedRow = PrepareRowForInsert(row, columns);
                dataTable.Rows.Add(preparedRow);
            }

            _logger.LogInformation("Using SqlBulkCopy for {Count} rows", data.Count);
            bulkCopy.WriteToServer(dataTable);

            return data.Count;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("SqlBulkCopy failed: {Message}, falling back to batch insert", ex.Message);
            return FastBatchImport(tableName, columns, data, 1000);
        }
    }

    private int FastBatchImport(string tableName, List<string> columns, List<object?[]> data, int batchSize)
    {
        var quotedColumns = columns.Select(col => $"[{col}]").ToList();
        var placeholders = string.Join(", ", columns.Select((_, i) => $"@p{i}"));
        var insertSql = $"INSERT INTO [{tableName}] ({string.Join(", ", quotedColumns)}) VALUES ({placeholders})";

        var totalImported = 0;

        for (int i = 0; i < data.Count; i += batchSize)
        {
            var batch = data.Skip(i).Take(batchSize).ToList();

            using var transaction = _connection!.BeginTransaction();
            try
            {
                foreach (var row in batch)
                {
                    using var command = new SqlCommand(insertSql, _connection, transaction);

                    var preparedRow = PrepareRowForInsert(row, columns);
                    for (int p = 0; p < preparedRow.Length; p++)
                    {
                        command.Parameters.AddWithValue($"@p{p}", preparedRow[p] ?? DBNull.Value);
                    }

                    command.ExecuteNonQuery();
                }

                transaction.Commit();
                totalImported += batch.Count;

                if (data.Count > 1000)
                {
                    _logger.LogDebug("Batch: {BatchCount} rows ({TotalImported}/{DataCount} total, {Percentage:F1}%)", batch.Count, totalImported, data.Count, (totalImported / (double)data.Count * 100));
                }
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        return totalImported;
    }

    private object[] PrepareRowForInsert(object?[] row, List<string> columns)
    {
        var preparedRow = new object[row.Length];

        for (int i = 0; i < row.Length; i++)
        {
            preparedRow[i] = ConvertValueForSqlServer(row[i]);
        }

        return preparedRow;
    }

    private object[] PrepareRowForInsertWithTypes(object?[] row, List<string> columnTypes)
    {
        var preparedRow = new object[row.Length];

        for (int i = 0; i < row.Length; i++)
        {
            preparedRow[i] = ConvertValueForSqlServerWithType(row[i], columnTypes[i]);
        }

        return preparedRow;
    }

    private object ConvertValueForSqlServer(object? value)
    {
        if (value == null || value == DBNull.Value)
            return DBNull.Value;

        // Handle string conversions
        if (value is string strValue)
        {
            // Only convert truly empty strings to DBNull, not whitespace
            // This preserves JSON strings and other data that might have whitespace
            if (strValue.Length == 0)
                return DBNull.Value;

            // Handle boolean-like values
            if (strValue.Equals("true", StringComparison.OrdinalIgnoreCase))
                return 1;
            if (strValue.Equals("false", StringComparison.OrdinalIgnoreCase))
                return 0;

            // Handle datetime values - SQL Server DATETIME supports 3 decimal places
            if (DateTimeHelper.IsLikelyIsoDateTime(strValue))
            {
                try
                {
                    // Remove timezone if present
                    var datetimePart = strValue.Contains('+') || strValue.EndsWith('Z') || strValue.Contains('T')
                        ? DateTimeHelper.RemoveTimezone(strValue) ?? strValue
                        : strValue;

                    // Handle microseconds - SQL Server DATETIME precision is 3.33ms, so truncate to 3 digits
                    if (datetimePart.Contains('.'))
                    {
                        var parts = datetimePart.Split('.');
                        if (parts.Length == 2 && parts[1].Length > 3)
                        {
                            datetimePart = $"{parts[0]}.{parts[1][..3]}";
                        }
                    }

                    return datetimePart;
                }
                catch
                {
                    // If conversion fails, return original value
                }
            }
        }

        return value;
    }

    private object ConvertValueForSqlServerWithType(object? value, string columnType)
    {
        if (value == null || value == DBNull.Value)
            return DBNull.Value;

        // Handle string conversions
        if (value is string strValue)
        {
            // Only convert truly empty strings to DBNull, not whitespace
            // This preserves JSON strings and other data that might have whitespace
            if (strValue.Length == 0)
                return DBNull.Value;

            // Handle GUID values - SqlBulkCopy requires actual Guid objects for UNIQUEIDENTIFIER columns
            // But NOT for NVARCHAR columns that happen to contain GUID strings
            if (columnType.Equals("uniqueidentifier", StringComparison.OrdinalIgnoreCase))
            {
                if (Guid.TryParse(strValue, out var guidValue))
                {
                    return guidValue;
                }
            }

            // Handle boolean-like values
            if (strValue.Equals("true", StringComparison.OrdinalIgnoreCase))
                return 1;
            if (strValue.Equals("false", StringComparison.OrdinalIgnoreCase))
                return 0;

            // Handle datetime values - SQL Server DATETIME supports 3 decimal places
            if (DateTimeHelper.IsLikelyIsoDateTime(strValue))
            {
                try
                {
                    // Remove timezone if present
                    var datetimePart = strValue.Contains('+') || strValue.EndsWith('Z') || strValue.Contains('T')
                        ? DateTimeHelper.RemoveTimezone(strValue) ?? strValue
                        : strValue;

                    // Handle microseconds - SQL Server DATETIME precision is 3.33ms, so truncate to 3 digits
                    if (datetimePart.Contains('.'))
                    {
                        var parts = datetimePart.Split('.');
                        if (parts.Length == 2 && parts[1].Length > 3)
                        {
                            datetimePart = $"{parts[0]}.{parts[1][..3]}";
                        }
                    }

                    return datetimePart;
                }
                catch
                {
                    // If conversion fails, return original value
                }
            }
        }

        return value;
    }

    public bool SupportsBulkCopy()
    {
        return true; // SQL Server SqlBulkCopy is highly optimized
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
            // Get actual table columns from SQL Server
            var actualColumns = GetTableColumns(tableName);
            if (actualColumns.Count == 0)
            {
                _logger.LogError("Could not retrieve columns for table {TableName}", tableName);
                return false;
            }

            // Filter columns and data
            var validColumnIndices = new List<int>();
            var validColumns = new List<string>();
            var missingColumns = new List<string>();

            for (int i = 0; i < columns.Count; i++)
            {
                if (actualColumns.Contains(columns[i]))
                {
                    validColumnIndices.Add(i);
                    validColumns.Add(columns[i]);
                }
                else
                {
                    missingColumns.Add(columns[i]);
                }
            }

            if (missingColumns.Count > 0)
            {
                _logger.LogWarning("Skipping columns that don't exist in {TableName}: {Columns}", tableName, string.Join(", ", missingColumns));
            }

            if (validColumns.Count == 0)
            {
                _logger.LogError("No valid columns found for table {TableName}", tableName);
                return false;
            }

            // Get column types for proper data conversion
            var columnTypes = GetTableColumnTypes(tableName);
            var validColumnTypes = validColumns.Select(col =>
                columnTypes.GetValueOrDefault(col, "nvarchar")).ToList();

            // Filter data to only include valid columns
            var filteredData = data.Select(row =>
                validColumnIndices.Select(i => i < row.Length ? row[i] : null).ToArray()
            ).ToList();

            // Check if table has identity columns
            var identityColumns = GetIdentityColumns(tableName);
            var identityColumnsInData = validColumns.Intersect(identityColumns).ToList();
            var needsIdentityInsert = identityColumnsInData.Count > 0;

            if (needsIdentityInsert)
            {
                _logger.LogInformation("Table {TableName} has identity columns in import data: {Columns}", tableName, string.Join(", ", identityColumnsInData));
            }

            _logger.LogInformation("Bulk importing {Count} rows into {TableName} using SqlBulkCopy", filteredData.Count, tableName);

            // Use SqlBulkCopy for high-performance import
            // When importing identity columns, we need SqlBulkCopyOptions.KeepIdentity
            var bulkCopyOptions = needsIdentityInsert
                ? SqlBulkCopyOptions.KeepIdentity
                : SqlBulkCopyOptions.Default;

            using var bulkCopy = new SqlBulkCopy(_connection, bulkCopyOptions, null)
            {
                DestinationTableName = $"[{tableName}]",
                BatchSize = 10000,
                BulkCopyTimeout = 600 // 10 minutes
            };

            // Map columns
            foreach (var column in validColumns)
            {
                bulkCopy.ColumnMappings.Add(column, column);
            }

            // Create DataTable
            var dataTable = new DataTable();
            foreach (var column in validColumns)
            {
                dataTable.Columns.Add(column, typeof(object));
            }

            // Add rows with data type conversion based on actual column types
            foreach (var row in filteredData)
            {
                var preparedRow = PrepareRowForInsertWithTypes(row, validColumnTypes);
                dataTable.Rows.Add(preparedRow);
            }

            bulkCopy.WriteToServer(dataTable);
            _logger.LogInformation("Successfully bulk imported {Count} rows into {TableName}", filteredData.Count, tableName);

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
            _logger.LogError("SQL Server import connection test failed: {Message}", ex.Message);
            return false;
        }
    }

    public void Dispose()
    {
        Disconnect();
    }
}
