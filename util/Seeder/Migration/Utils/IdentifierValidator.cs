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

    // Maximum reasonable length for identifiers (most databases have limits around 128-255)
    private const int MaxIdentifierLength = 128;

    /// <summary>
    /// Validates a SQL identifier (table name, column name, schema name).
    /// </summary>
    /// <param name="identifier">The identifier to validate</param>
    /// <returns>True if the identifier is valid, false otherwise</returns>
    public static bool IsValid(string? identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            return false;

        if (identifier.Length > MaxIdentifierLength)
            return false;

        return ValidIdentifierPattern.IsMatch(identifier);
    }

    /// <summary>
    /// Validates a SQL identifier and throws an exception if invalid.
    /// </summary>
    /// <param name="identifier">The identifier to validate</param>
    /// <param name="identifierType">The type of identifier (e.g., "table name", "column name")</param>
    /// <exception cref="ArgumentException">Thrown when the identifier is invalid</exception>
    public static void ValidateOrThrow(string? identifier, string identifierType = "identifier")
    {
        if (!IsValid(identifier))
        {
            throw new ArgumentException(
                $"Invalid {identifierType}: '{identifier}'. " +
                $"Identifiers must start with a letter or underscore, " +
                $"contain only letters, numbers, and underscores, " +
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
    /// <exception cref="ArgumentException">Thrown when any identifier is invalid</exception>
    public static void ValidateAllOrThrow(IEnumerable<string> identifiers, string identifierType = "identifiers")
    {
        foreach (var identifier in identifiers)
        {
            ValidateOrThrow(identifier, identifierType);
        }
    }

    /// <summary>
    /// Filters a list of identifiers to only include valid ones.
    /// </summary>
    /// <param name="identifiers">The identifiers to filter</param>
    /// <returns>A list of valid identifiers</returns>
    public static List<string> FilterValid(IEnumerable<string> identifiers)
    {
        return identifiers.Where(IsValid).ToList();
    }
}
