using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Bit.Scim.Models;

/// <summary>
/// Helpers for parsing and validating the subset of SCIM filter expressions
/// (RFC 7644 §3.4.2.2) that Bitwarden supports.
/// </summary>
/// <remarks>
/// Only simple <c>attribute eq "value"</c> expressions are supported. Compound
/// expressions (<c>and</c>/<c>or</c>/<c>not</c>), grouping, value paths and
/// operators other than <c>eq</c> are treated as unsupported.
/// </remarks>
public static partial class ScimFilterHelper
{
    // Matches a simple equality expression: <attribute> eq <value>
    // The value may be quoted ("value") or unquoted (value).
    [GeneratedRegex(@"^\s*(?<attr>[\w:.\-]+)\s+eq\s+(?<value>"".*""|.+?)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EqualityFilterRegex();

    // Detects logical operators (and/or/not) as whole tokens, grouping or value paths.
    [GeneratedRegex(@"[()\[\]]|(^|\s)(and|or|not)(\s|$)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UnsupportedConstructRegex();

    /// <summary>
    /// Attempts to parse a filter as a single supported <c>attribute eq "value"</c> expression.
    /// </summary>
    /// <param name="filter">The raw filter expression.</param>
    /// <param name="attribute">The parsed attribute name (lower-cased) when successful.</param>
    /// <param name="value">The parsed value (with surrounding quotes removed) when successful.</param>
    /// <returns><c>true</c> if the filter is a single equality expression; otherwise <c>false</c>.</returns>
    public static bool TryParseEqualityFilter(string filter, [NotNullWhen(true)] out string? attribute, [NotNullWhen(true)] out string? value)
    {
        attribute = null;
        value = null;

        if (string.IsNullOrWhiteSpace(filter))
        {
            return false;
        }

        // Reject compound expressions, grouping and value paths outright.
        if (UnsupportedConstructRegex().IsMatch(filter))
        {
            return false;
        }

        var match = EqualityFilterRegex().Match(filter);
        if (!match.Success)
        {
            return false;
        }

        attribute = match.Groups["attr"].Value.ToLowerInvariant();
        value = match.Groups["value"].Value.Trim('"');
        return true;
    }

    /// <summary>
    /// Builds a human-readable message describing why a SCIM filter expression is unsupported.
    /// When a single attribute can be identified, the message references it by name; otherwise
    /// it falls back to reporting the whole filter expression.
    /// </summary>
    public static string GetUnsupportedFilterMessage(string filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return "Filter expression is not supported.";
        }

        var trimmed = filter.Trim();

        // Compound expressions (and/or/not), grouping and value paths are not supported.
        if (UnsupportedConstructRegex().IsMatch(trimmed))
        {
            return $"Filter expression '{trimmed}' is not supported.";
        }

        var leadingAttribute = LeadingAttributeRegex().Match(trimmed);
        if (leadingAttribute.Success)
        {
            var attribute = leadingAttribute.Groups["attr"].Value;
            return $"Filter attribute '{attribute}' is not supported.";
        }

        return $"Filter expression '{trimmed}' is not supported.";
    }

    // Matches the leading attribute path of a filter, e.g. "active" in "active eq true".
    [GeneratedRegex(@"^\s*(?<attr>[\w:.\-]+)",
        RegexOptions.CultureInvariant)]
    private static partial Regex LeadingAttributeRegex();
}
