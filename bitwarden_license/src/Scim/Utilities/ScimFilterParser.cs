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
    /// Parses a SCIM filter string into attribute, operator, and value components.
    /// Returns false if the filter cannot be parsed.
    /// </summary>
    public static bool Parse(string filter, out string? attribute, out string? op, out string? value)
    {
        attribute = null;
        op = null;
        value = null;

        if (string.IsNullOrWhiteSpace(filter))
        {
            return false;
        }

        var filterLower = filter.ToLowerInvariant();

        foreach (var supportedOp in _supportedOperators)
        {
            var opPattern = $" {supportedOp} ";
            var opIndex = filterLower.IndexOf(opPattern, StringComparison.Ordinal);
            if (opIndex > 0)
            {
                attribute = filterLower[..opIndex].Trim();
                op = supportedOp;
                value = filter[(opIndex + opPattern.Length)..].Trim().Trim('"');
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Evaluates whether a field value matches the filter using the given operator.
    /// </summary>
    public static bool Matches(string fieldValue, string op, string filterValue)
    {
        if (fieldValue == null)
        {
            return op == "ne";
        }

        return op switch
        {
            "eq" => string.Equals(fieldValue, filterValue, StringComparison.OrdinalIgnoreCase),
            "ne" => !string.Equals(fieldValue, filterValue, StringComparison.OrdinalIgnoreCase),
            "co" => fieldValue.Contains(filterValue, StringComparison.OrdinalIgnoreCase),
            "sw" => fieldValue.StartsWith(filterValue, StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }
}
