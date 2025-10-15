using Microsoft.Extensions.Logging;

namespace Bit.Seeder.Migration;

public class TableFilter(
    List<string>? includeTables,
    List<string>? excludeTables,
    List<string>? permanentExclusions,
    ILogger<TableFilter> logger)
{
    private readonly ILogger<TableFilter> _logger = logger;
    private readonly HashSet<string> _includeTables = includeTables?.ToHashSet() ?? [];
    private readonly HashSet<string> _adHocExcludeTables = excludeTables?.ToHashSet() ?? [];
    private readonly HashSet<string> _permanentExclusions = permanentExclusions?.ToHashSet() ?? [];
    private readonly HashSet<string> _excludeTables = InitializeExcludeTables(excludeTables, permanentExclusions, includeTables, logger);

    private static HashSet<string> InitializeExcludeTables(
        List<string>? excludeTables,
        List<string>? permanentExclusions,
        List<string>? includeTables,
        ILogger<TableFilter> logger)
    {
        var adHocExcludeSet = excludeTables?.ToHashSet() ?? [];
        var permanentExcludeSet = permanentExclusions?.ToHashSet() ?? [];
        var includeSet = includeTables?.ToHashSet() ?? [];

        var result = new HashSet<string>(adHocExcludeSet);
        result.UnionWith(permanentExcludeSet);

        // Remove any permanently excluded tables from include list
        if (includeSet.Count > 0 && permanentExcludeSet.Count > 0)
        {
            var conflictingIncludes = includeSet.Intersect(permanentExcludeSet).ToList();
            if (conflictingIncludes.Count > 0)
            {
                logger.LogWarning("Removing permanently excluded tables from include list: {Tables}", string.Join(", ", conflictingIncludes.OrderBy(t => t)));
                includeSet.ExceptWith(conflictingIncludes);
            }
        }

        // Validate that both include and exclude aren't used together (for ad-hoc only)
        if (includeSet.Count > 0 && adHocExcludeSet.Count > 0)
        {
            logger.LogWarning("Both include and ad-hoc exclude tables specified. Include takes precedence over ad-hoc exclusions.");
            return new HashSet<string>(permanentExcludeSet);
        }

        return result;
    }

    public void LogFilterSetup()
    {
        if (_includeTables.Count > 0)
        {
            _logger.LogInformation("Table filter: INCLUDING only {Count} tables: {Tables}", _includeTables.Count, string.Join(", ", _includeTables.OrderBy(t => t)));
            if (_permanentExclusions.Count > 0)
            {
                _logger.LogInformation("Plus permanently excluding {Count} tables: {Tables}", _permanentExclusions.Count, string.Join(", ", _permanentExclusions.OrderBy(t => t)));
            }
        }
        else if (_excludeTables.Count > 0)
        {
            if (_permanentExclusions.Count > 0 && _adHocExcludeTables.Count > 0)
            {
                _logger.LogInformation("Table filter: EXCLUDING {Count} tables total:", _excludeTables.Count);
                _logger.LogInformation("  - Permanent exclusions: {Tables}", string.Join(", ", _permanentExclusions.OrderBy(t => t)));
                _logger.LogInformation("  - Ad-hoc exclusions: {Tables}", string.Join(", ", _adHocExcludeTables.OrderBy(t => t)));
            }
            else if (_permanentExclusions.Count > 0)
            {
                _logger.LogInformation("Table filter: EXCLUDING {Count} permanent tables: {Tables}", _permanentExclusions.Count, string.Join(", ", _permanentExclusions.OrderBy(t => t)));
            }
            else if (_adHocExcludeTables.Count > 0)
            {
                _logger.LogInformation("Table filter: EXCLUDING {Count} ad-hoc tables: {Tables}", _adHocExcludeTables.Count, string.Join(", ", _adHocExcludeTables.OrderBy(t => t)));
            }
        }
        else
        {
            _logger.LogInformation("Table filter: No filtering applied (processing all tables)");
        }
    }

    public bool ShouldProcessTable(string tableName)
    {
        // If include list is specified, only process tables in that list
        if (_includeTables.Count > 0)
        {
            var result = _includeTables.Contains(tableName);
            if (!result)
            {
                _logger.LogDebug("Skipping table {TableName} (not in include list)", tableName);
            }
            return result;
        }

        // If exclude list is specified, process all tables except those in the list
        if (_excludeTables.Count > 0)
        {
            var result = !_excludeTables.Contains(tableName);
            if (!result)
            {
                _logger.LogDebug("Skipping table {TableName} (in exclude list)", tableName);
            }
            return result;
        }

        // No filtering - process all tables
        return true;
    }

    public List<string> FilterTableList(List<string> allTables)
    {
        var originalCount = allTables.Count;
        var filteredTables = allTables.Where(ShouldProcessTable).ToList();

        _logger.LogInformation("Table filtering result: {FilteredCount}/{OriginalCount} tables selected for processing", filteredTables.Count, originalCount);

        if (_includeTables.Count > 0)
        {
            // Check if any requested include tables are missing
            var availableSet = new HashSet<string>(allTables);
            var missingTables = _includeTables.Except(availableSet).ToList();
            if (missingTables.Count > 0)
            {
                _logger.LogWarning("Requested tables not found: {Tables}", string.Join(", ", missingTables.OrderBy(t => t)));
                _logger.LogInformation("Available tables: {Tables}", string.Join(", ", allTables.OrderBy(t => t)));
            }
        }

        return filteredTables;
    }

    public string GetFilterDescription()
    {
        if (_includeTables.Count > 0)
        {
            var baseDesc = $"Including only: {string.Join(", ", _includeTables.OrderBy(t => t))}";
            if (_permanentExclusions.Count > 0)
            {
                baseDesc += $" (plus {_permanentExclusions.Count} permanent exclusions)";
            }
            return baseDesc;
        }

        if (_excludeTables.Count > 0)
        {
            if (_permanentExclusions.Count > 0 && _adHocExcludeTables.Count > 0)
            {
                return $"Excluding: {_permanentExclusions.Count} permanent + {_adHocExcludeTables.Count} ad-hoc tables";
            }
            if (_permanentExclusions.Count > 0)
            {
                return $"Excluding: {string.Join(", ", _permanentExclusions.OrderBy(t => t))} (permanent)";
            }
            return $"Excluding: {string.Join(", ", _excludeTables.OrderBy(t => t))}";
        }

        if (_permanentExclusions.Count > 0)
        {
            return $"No additional filtering (permanent exclusions: {string.Join(", ", _permanentExclusions.OrderBy(t => t))})";
        }

        return "No table filtering applied";
    }

    public bool ValidateTablesExist(List<string> availableTables)
    {
        var availableSet = new HashSet<string>(availableTables);
        var issuesFound = false;

        if (_includeTables.Count > 0)
        {
            var missingInclude = _includeTables.Except(availableSet).ToList();
            if (missingInclude.Count > 0)
            {
                _logger.LogError("Include tables not found: {Tables}", string.Join(", ", missingInclude.OrderBy(t => t)));
                issuesFound = true;
            }
        }

        if (_excludeTables.Count > 0)
        {
            var missingExclude = _excludeTables.Except(availableSet).ToList();
            if (missingExclude.Count > 0)
            {
                _logger.LogWarning("Exclude tables not found (will be ignored): {Tables}", string.Join(", ", missingExclude.OrderBy(t => t)));
            }
        }

        return !issuesFound;
    }

    public static List<string> ParseTableList(string? tableString)
    {
        if (string.IsNullOrWhiteSpace(tableString))
            return [];

        // Split by comma and trim whitespace
        return tableString.Split(',')
            .Select(t => t.Trim())
            .Where(t => !string.IsNullOrEmpty(t))
            .ToList();
    }

    public List<string>? GetIncludeTables() => _includeTables.Count > 0 ? _includeTables.ToList() : null;

    public List<string>? GetExcludeTables() => _adHocExcludeTables.Count > 0 ? _adHocExcludeTables.ToList() : null;
}
