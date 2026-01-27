using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace Bit.Core.Utilities;

/// <summary>
/// https://bitwarden.atlassian.net/browse/VULN-376
/// Domain names are vulnerable to XSS attacks if not properly validated.
/// Domain names can contain letters, numbers, dots, and hyphens.
/// Domain names maybe internationalized (IDN) and contain unicode characters.
/// </summary>
public class DomainNameValidatorAttribute : ValidationAttribute
{
    // RFC 1123 compliant domain name regex
    // - Allows alphanumeric characters and hyphens
    // - Cannot start or end with a hyphen
    // - Each label (part between dots) must be 1-63 characters
    // - Total length should not exceed 253 characters
    // - Supports internationalized domain names (IDN) - which is why this regex includes unicode ranges
    private static readonly Regex _domainNameRegex = new(
        @"^(?:[a-zA-Z0-9\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF](?:[a-zA-Z0-9\-\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF]{0,61}[a-zA-Z0-9\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])?\.)*[a-zA-Z0-9\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF](?:[a-zA-Z0-9\-\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF]{0,61}[a-zA-Z0-9\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );

    public DomainNameValidatorAttribute()
        : base("The {0} field is not a valid domain name.")
    { }

    public override bool IsValid(object? value)
    {
        if (value == null)
        {
            return true; // Use [Required] for null checks
        }

        var domainName = value.ToString();

        if (string.IsNullOrWhiteSpace(domainName))
        {
            return false;
        }

        // Reject if contains any whitespace (including leading/trailing spaces, tabs, newlines)
        if (domainName.Any(char.IsWhiteSpace))
        {
            return false;
        }

        // Check length constraints
        if (domainName.Length > 253)
        {
            return false;
        }

        // Check for control characters or other dangerous characters
        if (domainName.Any(c => char.IsControl(c) || c == '<' || c == '>' || c == '"' || c == '\'' || c == '&'))
        {
            return false;
        }

        // Validate against domain name regex
        return _domainNameRegex.IsMatch(domainName);
    }
}
