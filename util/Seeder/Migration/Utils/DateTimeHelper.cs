namespace Bit.Seeder.Migration.Utils;

public static class DateTimeHelper
{
    /// <summary>
    /// Checks if a string looks like an ISO datetime format (YYYY-MM-DD or variations with time).
    /// This prevents false positives with data containing '+' signs (like base64 encoded strings).
    /// </summary>
    public static bool IsLikelyIsoDateTime(string value)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        // Must have reasonable length for a datetime string
        if (value.Length < 10 || value.Length > 35)
            return false;

        // Must start with a digit
        if (!char.IsDigit(value[0]))
            return false;

        // Must contain a dash (date separator)
        if (!value.Contains('-'))
            return false;

        // Dash should be at position 2 (MM-DD-...) or 4 (YYYY-MM-...)
        var dashIndex = value.IndexOf('-');
        if (dashIndex != 2 && dashIndex != 4)
            return false;

        // Must have either 'T' separator or both ':' and '-' for datetime
        return value.Contains('T') || (value.Contains(':') && value.Contains('-'));
    }

    /// <summary>
    /// Extracts the datetime portion from an ISO datetime string, removing timezone info.
    /// </summary>
    public static string? RemoveTimezone(string value)
    {
        if (string.IsNullOrEmpty(value))
            return null;

        var result = value;

        // Remove timezone offset (e.g., +00:00, -05:00)
        if (result.Contains('+'))
            result = result.Split('+')[0];
        else if (result.EndsWith('Z'))
            result = result[..^1];

        // Convert ISO 'T' separator to space for SQL compatibility
        result = result.Replace('T', ' ');

        return result;
    }
}
