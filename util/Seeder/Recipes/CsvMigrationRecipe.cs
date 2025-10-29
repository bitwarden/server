using Bit.Seeder.Migration;
using Bit.Seeder.Migration.Databases;
using Bit.Seeder.Migration.Models;
using Bit.Seeder.Migration.Reporters;
using Bit.Seeder.Migration.Utils;
using Microsoft.Extensions.Logging;

namespace Bit.Seeder.Recipes;

public class CsvMigrationRecipe(MigrationConfig config, ILoggerFactory loggerFactory)
{
    private readonly ILogger<CsvMigrationRecipe> _logger = loggerFactory.CreateLogger<CsvMigrationRecipe>();
    private readonly MigrationConfig _config = config;
    private readonly ILoggerFactory _loggerFactory = loggerFactory;
    private readonly SchemaMapper _schemaMapper = new(
        config.TableMappings,
        config.SpecialColumns,
        loggerFactory.CreateLogger<SchemaMapper>());
    private readonly CsvHandler _csvHandler = new(
        config.CsvSettings,
        loggerFactory.CreateLogger<CsvHandler>());
    private SshTunnel? _sshTunnel;
    private SqlServerExporter? _sourceExporter;

    // Separator constants for logging
    private const string Separator = "================================================================================";
    private const string ShortSeparator = "----------------------------------------";

    public bool StartSshTunnel(bool force = false)
    {
        if (!force && !_config.SshTunnel.Enabled)
        {
            _logger.LogInformation("SSH tunnel not enabled in configuration");
            return true;
        }

        try
        {
            _logger.LogInformation("Starting SSH tunnel to {RemoteHost}...", _config.SshTunnel.RemoteHost);
            _sshTunnel = new SshTunnel(
                _config.SshTunnel.RemoteHost,
                _config.SshTunnel.RemoteUser,
                _config.SshTunnel.LocalPort,
                _config.SshTunnel.RemotePort,
                _config.SshTunnel.PrivateKeyPath,
                _config.SshTunnel.PrivateKeyPassphrase,
                _loggerFactory.CreateLogger<SshTunnel>());

            return _sshTunnel.StartTunnel();
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to start SSH tunnel: {Message}", ex.Message);
            return false;
        }
    }

    public void StopSshTunnel()
    {
        if (_sshTunnel != null)
        {
            _sshTunnel.StopTunnel();
            _sshTunnel.Dispose();
            _sshTunnel = null;
        }
    }

    public bool DiscoverAndAnalyzeTables()
    {
        if (_config.Source == null)
        {
            _logger.LogError("Source database not configured");
            return false;
        }

        try
        {
            _sourceExporter = new SqlServerExporter(
                _config.Source,
                _loggerFactory.CreateLogger<SqlServerExporter>());

            if (!_sourceExporter.Connect())
            {
                _logger.LogError("Failed to connect to source database");
                return false;
            }

            var tables = _sourceExporter.DiscoverTables();
            _logger.LogInformation("\nDiscovered {Count} tables:", tables.Count);

            var patterns = _schemaMapper.DetectNamingPatterns(tables);
            var suggestions = _schemaMapper.SuggestTableMappings(tables);

            _logger.LogInformation("\nTable Details:");
            _logger.LogInformation(Separator);
            _logger.LogInformation("{Header1,-30} {Header2,10} {Header3,15} {Header4,15}", "Table Name", "Columns", "Rows", "Special Cols");
            _logger.LogInformation(Separator);

            foreach (var tableName in tables.OrderBy(t => t))
            {
                var tableInfo = _sourceExporter.GetTableInfo(tableName);
                var jsonColumns = _sourceExporter.IdentifyJsonColumns(tableName, 100);

                _logger.LogInformation("{TableName,-30} {ColumnCount,10} {RowCount,15:N0} {JsonColumnCount,15}", tableName, tableInfo.Columns.Count, tableInfo.RowCount, jsonColumns.Count);

                if (jsonColumns.Count > 0)
                {
                    _logger.LogInformation("  → JSON columns: {JsonColumns}", string.Join(", ", jsonColumns));
                }
            }

            _logger.LogInformation(Separator);

            _sourceExporter.Disconnect();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError("Error during table discovery: {Message}", ex.Message);
            return false;
        }
    }

    public bool ExportAllTables(TableFilter? tableFilter = null)
    {
        if (_config.Source == null)
        {
            _logger.LogError("Source database not configured");
            return false;
        }

        try
        {
            _sourceExporter = new SqlServerExporter(
                _config.Source,
                _loggerFactory.CreateLogger<SqlServerExporter>());

            if (!_sourceExporter.Connect())
            {
                _logger.LogError("Failed to connect to source database");
                return false;
            }

            var reporter = new ExportReporter(_loggerFactory.CreateLogger<ExportReporter>());
            var allTables = _sourceExporter.DiscoverTables();

            TableFilter effectiveFilter = tableFilter != null
                ? new TableFilter(
                    tableFilter.GetIncludeTables(),
                    tableFilter.GetExcludeTables(),
                    _config.ExcludeTables,
                    _loggerFactory.CreateLogger<TableFilter>())
                : new TableFilter(
                    null,
                    null,
                    _config.ExcludeTables,
                    _loggerFactory.CreateLogger<TableFilter>());

            var tablesToExport = effectiveFilter.FilterTableList(allTables);

            reporter.StartExport();
            _logger.LogInformation("Exporting {Count} tables to CSV\n", tablesToExport.Count);

            foreach (var tableName in tablesToExport)
            {
                reporter.StartTable(tableName);

                try
                {
                    var (columns, data) = _sourceExporter.ExportTableData(tableName, _config.BatchSize);
                    var specialColumns = _sourceExporter.IdentifyJsonColumns(tableName);
                    var csvPath = _csvHandler.ExportTableToCsv(tableName, columns, data.ToList(), specialColumns);

                    if (_csvHandler.ValidateExport(data.Count, csvPath))
                    {
                        reporter.FinishTable(ExportStatus.Success, data.Count);
                    }
                    else
                    {
                        reporter.FinishTable(ExportStatus.Failed, 0, "Export validation failed");
                    }
                }
                catch (Exception ex)
                {
                    reporter.FinishTable(ExportStatus.Failed, 0, ex.Message);
                }
            }

            reporter.FinishExport();
            _sourceExporter.Disconnect();
            return reporter.GetSummaryStats().FailedTables == 0;
        }
        catch (Exception ex)
        {
            _logger.LogError("Error during export: {Message}", ex.Message);
            return false;
        }
    }

    public bool ImportToDatabase(
        string dbType,
        bool createTables = false,
        bool clearExisting = false,
        TableFilter? tableFilter = null,
        int? batchSize = null)
    {
        try
        {
            if (!_config.Destinations.TryGetValue(dbType, out var destConfig))
            {
                _logger.LogError("Database type '{DbType}' not found in configuration", dbType);
                return false;
            }

            IDatabaseImporter? importer = CreateImporter(dbType, destConfig);
            if (importer == null)
            {
                _logger.LogError("Failed to create importer for {DbType}", dbType);
                return false;
            }

            if (!importer.Connect())
            {
                _logger.LogError("Failed to connect to {DbType} database", dbType);
                return false;
            }

            var reporter = new ImportReporter(_loggerFactory.CreateLogger<ImportReporter>());
            reporter.StartImport();

            importer.DisableForeignKeys();

            var csvFiles = Directory.GetFiles(_config.CsvSettings.OutputDir, "*.csv");
            var tableNames = csvFiles.Select(f => Path.GetFileNameWithoutExtension(f))
                .OrderBy(name => name)
                .ToList();

            TableFilter effectiveFilter = tableFilter != null
                ? new TableFilter(
                    tableFilter.GetIncludeTables(),
                    tableFilter.GetExcludeTables(),
                    _config.ExcludeTables,
                    _loggerFactory.CreateLogger<TableFilter>())
                : new TableFilter(
                    null,
                    null,
                    _config.ExcludeTables,
                    _loggerFactory.CreateLogger<TableFilter>());

            var tablesToImport = effectiveFilter.FilterTableList(tableNames);
            _logger.LogInformation("\nImporting {Count} tables to {DbType}", tablesToImport.Count, dbType);

            foreach (var tableName in tablesToImport)
            {
                var csvPath = Path.Combine(_config.CsvSettings.OutputDir, $"{tableName}.csv");

                if (!File.Exists(csvPath))
                {
                    _logger.LogWarning("CSV file not found for table {TableName}, skipping", tableName);
                    continue;
                }

                try
                {
                    var (columns, data) = _csvHandler.ImportCsvToData(
                        csvPath,
                        _schemaMapper.GetSpecialColumnsForTable(tableName));

                    var destTableName = _schemaMapper.GetDestinationTableName(tableName, dbType);
                    reporter.StartTable(tableName, destTableName, data.Count);

                    var tableExists = importer.TableExists(destTableName);

                    if (!tableExists && !createTables)
                    {
                        reporter.FinishTable(ImportStatus.Skipped, 0,
                            errorMessage: "Table does not exist and --create-tables not specified");
                        continue;
                    }

                    if (clearExisting && tableExists)
                    {
                        _logger.LogInformation("Clearing existing data from {DestTableName}", destTableName);
                        importer.DropTable(destTableName);
                        tableExists = false;
                    }

                    if (!tableExists && createTables)
                    {
                        var tableInfo = CreateBasicTableInfo(tableName, columns, data);
                        var specialColumns = _schemaMapper.GetSpecialColumnsForTable(tableName);

                        if (!importer.CreateTableFromSchema(
                            destTableName,
                            tableInfo.Columns,
                            tableInfo.ColumnTypes,
                            specialColumns))
                        {
                            reporter.FinishTable(ImportStatus.Failed, 0,
                                errorMessage: "Failed to create table");
                            continue;
                        }
                    }

                    var effectiveBatchSize = batchSize ?? _config.BatchSize;
                    var success = importer.ImportData(destTableName, columns, data, effectiveBatchSize);

                    if (success)
                    {
                        var actualCount = importer.GetTableRowCount(destTableName);
                        reporter.FinishTable(ImportStatus.Success, actualCount);
                    }
                    else
                    {
                        reporter.FinishTable(ImportStatus.Failed, 0,
                            errorMessage: "Import operation failed");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError("Error importing {TableName}: {Message}", tableName, ex.Message);
                    reporter.FinishTable(ImportStatus.Failed, 0, errorMessage: ex.Message);
                }
            }

            importer.EnableForeignKeys();
            reporter.FinishImport();

            var logsDir = "logs";
            Directory.CreateDirectory(logsDir);
            var reportPath = Path.Combine(logsDir,
                $"import_report_{dbType}_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
            reporter.ExportReport(reportPath);

            importer.Disconnect();

            var summary = reporter.GetSummaryStats();
            return summary.FailedTables == 0;
        }
        catch (Exception ex)
        {
            _logger.LogError("Error during import: {Message}", ex.Message);
            return false;
        }
    }

    public bool VerifyImport(string dbType, TableFilter? tableFilter = null)
    {
        try
        {
            if (!_config.Destinations.TryGetValue(dbType, out var destConfig))
            {
                _logger.LogError("Database type '{DbType}' not found in configuration", dbType);
                return false;
            }

            IDatabaseImporter? importer = CreateImporter(dbType, destConfig);
            if (importer == null)
            {
                _logger.LogError("Failed to create importer for {DbType}", dbType);
                return false;
            }

            if (!importer.Connect())
            {
                _logger.LogError("Failed to connect to {DbType} database", dbType);
                return false;
            }

            var reporter = new VerificationReporter(_loggerFactory.CreateLogger<VerificationReporter>());
            reporter.StartVerification();

            var csvFiles = Directory.GetFiles(_config.CsvSettings.OutputDir, "*.csv");
            var tableNames = csvFiles.Select(f => Path.GetFileNameWithoutExtension(f))
                .OrderBy(name => name)
                .ToList();

            TableFilter effectiveFilter = tableFilter != null
                ? new TableFilter(
                    tableFilter.GetIncludeTables(),
                    tableFilter.GetExcludeTables(),
                    _config.ExcludeTables,
                    _loggerFactory.CreateLogger<TableFilter>())
                : new TableFilter(
                    null,
                    null,
                    _config.ExcludeTables,
                    _loggerFactory.CreateLogger<TableFilter>());

            var tablesToVerify = effectiveFilter.FilterTableList(tableNames);
            _logger.LogInformation("\nVerifying {Count} tables in {DbType}", tablesToVerify.Count, dbType);

            foreach (var tableName in tablesToVerify)
            {
                var csvPath = Path.Combine(_config.CsvSettings.OutputDir, $"{tableName}.csv");

                if (!File.Exists(csvPath))
                {
                    reporter.VerifyTable(tableName, tableName, -1, 0,
                        errorMessage: "CSV file not found");
                    continue;
                }

                try
                {
                    var csvRowCount = CountCsvRows(csvPath);
                    var destTableName = _schemaMapper.GetDestinationTableName(tableName, dbType);

                    if (!importer.TableExists(destTableName))
                    {
                        reporter.VerifyTable(tableName, destTableName, csvRowCount, 0,
                            errorMessage: "Table does not exist in database");
                        continue;
                    }

                    var dbRowCount = importer.GetTableRowCount(destTableName);
                    reporter.VerifyTable(tableName, destTableName, csvRowCount, dbRowCount);
                }
                catch (Exception ex)
                {
                    _logger.LogError("Error verifying {TableName}: {Message}", tableName, ex.Message);
                    reporter.VerifyTable(tableName, tableName, -1, 0, errorMessage: ex.Message);
                }
            }

            reporter.FinishVerification();

            var logsDir = "logs";
            Directory.CreateDirectory(logsDir);
            var reportPath = Path.Combine(logsDir,
                $"verification_report_{dbType}_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
            reporter.ExportReport(reportPath);

            importer.Disconnect();

            var summary = reporter.GetSummaryStats();
            return summary.MismatchedTables == 0 && summary.ErrorTables == 0 && summary.MissingTables == 0;
        }
        catch (Exception ex)
        {
            _logger.LogError("Error during verification: {Message}", ex.Message);
            return false;
        }
    }

    public bool TestConnection(string dbType)
    {
        try
        {
            if (!_config.Destinations.TryGetValue(dbType, out var destConfig))
            {
                _logger.LogError("Database type '{DbType}' not found in configuration", dbType);
                return false;
            }

            IDatabaseImporter? importer = CreateImporter(dbType, destConfig);
            if (importer == null)
            {
                _logger.LogError("Failed to create importer for {DbType}", dbType);
                return false;
            }

            _logger.LogInformation("Testing connection to {DbType}...", dbType);
            var result = importer.TestConnection();

            if (result)
            {
                _logger.LogInformation("✓ Connection to {DbType} successful!", dbType);
            }
            else
            {
                _logger.LogError("✗ Connection to {DbType} failed", dbType);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError("Connection test failed: {Message}", ex.Message);
            return false;
        }
    }

    private IDatabaseImporter? CreateImporter(string dbType, DatabaseConfig config) =>
        dbType.ToLower() switch
        {
            "postgres" or "postgresql" => new PostgresImporter(config, _loggerFactory.CreateLogger<PostgresImporter>()),
            "mariadb" or "mysql" => new MariaDbImporter(config, _loggerFactory.CreateLogger<MariaDbImporter>()),
            "sqlite" => new SqliteImporter(config, _loggerFactory.CreateLogger<SqliteImporter>()),
            "sqlserver" or "mssql" => new SqlServerImporter(config, _loggerFactory.CreateLogger<SqlServerImporter>()),
            _ => null
        };

    private static TableInfo CreateBasicTableInfo(string tableName, List<string> columns, List<object[]> data)
    {
        var columnTypes = new Dictionary<string, string>();

        for (int i = 0; i < columns.Count; i++)
        {
            var columnName = columns[i];
            var sampleValue = data.FirstOrDefault()?[i];

            var inferredType = sampleValue switch
            {
                null => "NVARCHAR(MAX)",
                int => "INT",
                long => "BIGINT",
                double or float or decimal => "DECIMAL(18,6)",
                bool => "BIT",
                DateTime => "DATETIME2",
                byte[] => "VARBINARY(MAX)",
                _ => "NVARCHAR(MAX)"
            };

            columnTypes[columnName] = inferredType + " NULL";
        }

        return new TableInfo
        {
            Name = tableName,
            Columns = columns,
            ColumnTypes = columnTypes,
            RowCount = data.Count
        };
    }

    private int CountCsvRows(string csvPath)
    {
        var lineCount = 0;
        using (var reader = new StreamReader(csvPath))
        {
            while (reader.ReadLine() != null)
            {
                lineCount++;
            }
        }

        if (_config.CsvSettings.IncludeHeaders)
        {
            lineCount--;
        }

        return lineCount;
    }
}
