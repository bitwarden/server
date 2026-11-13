namespace Bit.Scim.Utilities;

/// <summary>
/// Parses simple SCIM filter expressions per RFC 7644 §3.4.2.2.
/// Supports: attribute op value (e.g., "userName eq \"john\"")
/// Operators: eq, ne, co, sw
/// </summary>
public static class ScimFilterParser
{
    private static readonly string[] _supportedOperators = { "eq", "ne", "co", "sw" };

    /// <summary>
    /// Parses a SCIM filter string and returns a predicate that can be used to filter results.
    /// Returns null if the filter cannot be parsed or the attribute is not in the provided map.
    /// </summary>
    /// <param name="filter">Raw SCIM filter string (e.g., "userName eq \"john\"").</param>
    /// <param name="attributeSelectors">Map of lowercase attribute names to (field selector, string comparison) tuples.</param>
    public static Func<T, bool>? TryGetPredicate<T>(
        string? filter,
        Dictionary<string, (Func<T, string?> Selector, StringComparison Comparison)> attributeSelectors)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return null;
        }

        var parts = filter.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3)
        {
            return null;
        }

        var candidateOp = parts[1].ToLowerInvariant();
        if (!_supportedOperators.Contains(candidateOp))
        {
            return null;
        }

        var attribute = parts[0].ToLowerInvariant();
        var filterValue = parts[2].Trim().Trim('"');

        if (!attributeSelectors.TryGetValue(attribute, out var entry))
        {
            return null;
        }

        return item => Matches(entry.Selector(item), candidateOp, filterValue, entry.Comparison);
    }

    private static bool Matches(string? fieldValue, string op, string filterValue, StringComparison comparison)
    {
        if (fieldValue == null)
        {
            return op == "ne";
        }

        return op switch
        {
            "eq" => string.Equals(fieldValue, filterValue, comparison),
            "ne" => !string.Equals(fieldValue, filterValue, comparison),
            "co" => fieldValue.Contains(filterValue, comparison),
            "sw" => fieldValue.StartsWith(filterValue, comparison),
            _ => false
        };
    }
}
