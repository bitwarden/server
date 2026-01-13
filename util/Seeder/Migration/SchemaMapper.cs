using Microsoft.Extensions.Logging;

namespace Bit.Seeder.Migration;

public class SchemaMapper(
    Dictionary<string, string> tableMappings,
    Dictionary<string, List<string>> specialColumns,
    ILogger<SchemaMapper> logger)
{
    private readonly ILogger<SchemaMapper> _logger = logger;
    private readonly Dictionary<string, string> _tableMappings = tableMappings ?? [];
    private readonly Dictionary<string, List<string>> _specialColumns = specialColumns ?? [];
    private readonly Dictionary<string, string> _reverseMappings = (tableMappings ?? []).ToDictionary(kv => kv.Value, kv => kv.Key);

    public string GetDestinationTableName(string sourceTable, string? destinationDbType = null)
    {
        // For SQL Server to SQL Server, don't apply table mappings (schema is identical)
        if (destinationDbType == "sqlserver")
        {
            _logger.LogDebug("SQL Server destination: keeping original table name {SourceTable}", sourceTable);
            return sourceTable;
        }

        // For other databases, apply configured mappings
        var mappedName = _tableMappings.GetValueOrDefault(sourceTable, sourceTable);

        if (mappedName != sourceTable)
        {
            _logger.LogDebug("Mapped table {SourceTable} -> {MappedName} for {DestinationDbType}", sourceTable, mappedName, destinationDbType);
        }

        return mappedName;
    }

    public string GetSourceTableName(string destinationTable) =>
        _reverseMappings.GetValueOrDefault(destinationTable, destinationTable);

    public List<string> GetSpecialColumnsForTable(string tableName)
    {
        var specialCols = _specialColumns.GetValueOrDefault(tableName, []);

        if (specialCols.Count > 0)
        {
            _logger.LogDebug("Table {TableName} has special columns: {Columns}", tableName, string.Join(", ", specialCols));
        }

        return specialCols;
    }

    public bool IsSpecialColumn(string tableName, string columnName)
    {
        var specialCols = GetSpecialColumnsForTable(tableName);
        return specialCols.Contains(columnName);
    }

    public Dictionary<string, string> SuggestTableMappings(List<string> sourceTables)
    {
        var suggestions = new Dictionary<string, string>();

        foreach (var table in sourceTables)
        {
            // Check if table is singular and suggest plural
            if (!table.EndsWith("s") && !table.EndsWith("es"))
            {
                string suggested;

                if (table.EndsWith("y"))
                {
                    // Company -> Companies
                    suggested = table[..^1] + "ies";
                }
                else if (table.EndsWith("s") || table.EndsWith("sh") || table.EndsWith("ch") ||
                         table.EndsWith("x") || table.EndsWith("z"))
                {
                    // Class -> Classes, Box -> Boxes
                    suggested = table + "es";
                }
                else if (table.EndsWith("f"))
                {
                    // Shelf -> Shelves
                    suggested = table[..^1] + "ves";
                }
                else if (table.EndsWith("fe"))
                {
                    // Life -> Lives
                    suggested = table[..^2] + "ves";
                }
                else
                {
                    // User -> Users
                    suggested = table + "s";
                }

                suggestions[table] = suggested;
            }
        }

        if (suggestions.Count > 0)
        {
            _logger.LogInformation("Suggested table mappings (singular -> plural):");
            foreach (var (source, dest) in suggestions)
            {
                _logger.LogInformation("  {Source} -> {Dest}", source, dest);
            }
        }

        return suggestions;
    }

    public bool ValidateMappings(List<string> sourceTables)
    {
        var sourceSet = new HashSet<string>(sourceTables);
        var invalidMappings = new List<string>();

        foreach (var sourceTable in _tableMappings.Keys)
        {
            if (!sourceSet.Contains(sourceTable))
            {
                invalidMappings.Add(sourceTable);
            }
        }

        if (invalidMappings.Count > 0)
        {
            _logger.LogError("Invalid table mappings found: {InvalidMappings}", string.Join(", ", invalidMappings));
            _logger.LogError("Available source tables: {SourceTables}", string.Join(", ", sourceTables.OrderBy(t => t)));
            return false;
        }

        _logger.LogInformation("All table mappings are valid");
        return true;
    }

    public void AddMapping(string sourceTable, string destinationTable)
    {
        _tableMappings[sourceTable] = destinationTable;
        _reverseMappings[destinationTable] = sourceTable;
        _logger.LogInformation("Added mapping: {SourceTable} -> {DestinationTable}", sourceTable, destinationTable);
    }

    public void AddSpecialColumns(string tableName, List<string> columns)
    {
        if (!_specialColumns.ContainsKey(tableName))
        {
            _specialColumns[tableName] = [];
        }

        _specialColumns[tableName].AddRange(columns);
        // Remove duplicates
        _specialColumns[tableName] = _specialColumns[tableName].Distinct().ToList();

        _logger.LogInformation("Added special columns to {TableName}: {Columns}", tableName, string.Join(", ", columns));
    }

    public Dictionary<string, List<string>> DetectNamingPatterns(List<string> tableNames)
    {
        var patterns = new Dictionary<string, List<string>>
        {
            ["singular"] = [],
            ["plural"] = [],
            ["mixed_case"] = [],
            ["snake_case"] = [],
            ["camel_case"] = [],
            ["all_caps"] = [],
            ["all_lower"] = []
        };

        foreach (var table in tableNames)
        {
            // Case patterns
            if (table.All(char.IsUpper))
                patterns["all_caps"].Add(table);
            else if (table.All(char.IsLower))
                patterns["all_lower"].Add(table);
            else if (table.Contains('_'))
                patterns["snake_case"].Add(table);
            else if (table.Skip(1).Any(char.IsUpper))
                patterns["camel_case"].Add(table);
            else
                patterns["mixed_case"].Add(table);

            // Singular/plural detection (simple heuristic)
            if (table.EndsWith("s") || table.EndsWith("es") || table.EndsWith("ies"))
                patterns["plural"].Add(table);
            else
                patterns["singular"].Add(table);
        }

        // Log pattern analysis
        _logger.LogInformation("Table naming pattern analysis:");
        foreach (var (pattern, tables) in patterns.Where(p => p.Value.Count > 0))
        {
            var preview = string.Join(", ", tables.Take(3));
            var ellipsis = tables.Count > 3 ? "..." : "";
            _logger.LogInformation("  {Pattern}: {Count} tables - {Preview}{Ellipsis}", pattern, tables.Count, preview, ellipsis);
        }

        return patterns;
    }

    public void LogInitialization()
    {
        _logger.LogInformation("Initialized schema mapper with {Count} table mappings", _tableMappings.Count);
        foreach (var (source, dest) in _tableMappings)
        {
            _logger.LogInformation("  {Source} -> {Dest}", source, dest);
        }
    }
}
