using System.Text.RegularExpressions;

namespace Bit.Seeder.Migration.Utils;

/// <summary>
/// Validates SQL identifiers (table names, column names, schema names) to prevent SQL injection.
/// </summary>
public static class IdentifierValidator
{
    // Regex pattern for valid SQL identifiers: must start with letter or underscore,
    // followed by letters, numbers, or underscores
    private static readonly Regex ValidIdentifierPattern = new Regex(
        @"^[a-zA-Z_][a-zA-Z0-9_]*$",
        RegexOptions.Compiled
    );

    // Regex pattern for more restrictive validation (no leading underscores)
    private static readonly Regex RestrictiveIdentifierPattern = new Regex(
        @"^[a-zA-Z][a-zA-Z0-9_]*$",
        RegexOptions.Compiled
    );

    // Maximum reasonable length for identifiers (most databases have limits around 128-255)
    private const int MaxIdentifierLength = 128;

    // SQL reserved keywords that should be rejected (common across SQL Server and PostgreSQL)
    // SECURITY: Prevent identifiers that match SQL keywords to avoid injection attempts
    private static readonly HashSet<string> SqlReservedKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "SELECT", "INSERT", "UPDATE", "DELETE", "DROP", "CREATE", "ALTER", "EXEC", "EXECUTE",
        "TABLE", "DATABASE", "SCHEMA", "INDEX", "VIEW", "PROCEDURE", "FUNCTION", "TRIGGER",
        "WHERE", "FROM", "JOIN", "UNION", "ORDER", "GROUP", "HAVING", "INTO", "VALUES",
        "SET", "AND", "OR", "NOT", "NULL", "IS", "AS", "ON", "IN", "EXISTS", "BETWEEN",
        "LIKE", "CASE", "WHEN", "THEN", "ELSE", "END", "BEGIN", "COMMIT", "ROLLBACK",
        "GRANT", "REVOKE", "DECLARE", "CAST", "CONVERT", "TRUNCATE", "MERGE"
    };

    /// <summary>
    /// Validates a SQL identifier (table name, column name, schema name).
    /// </summary>
    /// <param name="identifier">The identifier to validate</param>
    /// <param name="useRestrictiveMode">If true, disallow leading underscores and SQL reserved keywords</param>
    /// <returns>True if the identifier is valid, false otherwise</returns>
    public static bool IsValid(string? identifier, bool useRestrictiveMode = false)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            return false;

        if (identifier.Length > MaxIdentifierLength)
            return false;

        // SECURITY: In restrictive mode, reject SQL reserved keywords
        if (useRestrictiveMode && SqlReservedKeywords.Contains(identifier))
            return false;

        // Use appropriate pattern based on mode
        var pattern = useRestrictiveMode ? RestrictiveIdentifierPattern : ValidIdentifierPattern;
        return pattern.IsMatch(identifier);
    }

    /// <summary>
    /// Validates a SQL identifier and throws an exception if invalid.
    /// </summary>
    /// <param name="identifier">The identifier to validate</param>
    /// <param name="identifierType">The type of identifier (e.g., "table name", "column name")</param>
    /// <param name="useRestrictiveMode">If true, disallow leading underscores and SQL reserved keywords</param>
    /// <exception cref="ArgumentException">Thrown when the identifier is invalid</exception>
    public static void ValidateOrThrow(string? identifier, string identifierType = "identifier", bool useRestrictiveMode = false)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            throw new ArgumentException(
                $"Invalid {identifierType}: identifier is null or empty.",
                nameof(identifier)
            );
        }

        if (useRestrictiveMode && SqlReservedKeywords.Contains(identifier))
        {
            throw new ArgumentException(
                $"Invalid {identifierType}: '{identifier}' is a SQL reserved keyword.",
                nameof(identifier)
            );
        }

        if (!IsValid(identifier, useRestrictiveMode))
        {
            var requirements = useRestrictiveMode
                ? "must start with a letter (no underscores), contain only letters, numbers, and underscores"
                : "must start with a letter or underscore, contain only letters, numbers, and underscores";

            throw new ArgumentException(
                $"Invalid {identifierType}: '{identifier}'. " +
                $"Identifiers {requirements}, " +
                $"and be no longer than {MaxIdentifierLength} characters.",
                nameof(identifier)
            );
        }
    }

    /// <summary>
    /// Validates multiple identifiers and throws an exception if any are invalid.
    /// </summary>
    /// <param name="identifiers">The identifiers to validate</param>
    /// <param name="identifierType">The type of identifiers (e.g., "column names")</param>
    /// <param name="useRestrictiveMode">If true, disallow leading underscores and SQL reserved keywords</param>
    /// <exception cref="ArgumentException">Thrown when any identifier is invalid</exception>
    public static void ValidateAllOrThrow(IEnumerable<string> identifiers, string identifierType = "identifiers", bool useRestrictiveMode = false)
    {
        foreach (var identifier in identifiers)
        {
            ValidateOrThrow(identifier, identifierType, useRestrictiveMode);
        }
    }

    /// <summary>
    /// Filters a list of identifiers to only include valid ones.
    /// </summary>
    /// <param name="identifiers">The identifiers to filter</param>
    /// <param name="useRestrictiveMode">If true, disallow leading underscores and SQL reserved keywords</param>
    /// <returns>A list of valid identifiers</returns>
    public static List<string> FilterValid(IEnumerable<string> identifiers, bool useRestrictiveMode = false)
    {
        return identifiers.Where(id => IsValid(id, useRestrictiveMode)).ToList();
    }
}
