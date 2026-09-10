using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace Bit.Core.Utilities;

/// <summary>
/// https://bitwarden.atlassian.net/browse/VULN-376
/// Domain names are vulnerable to XSS attacks if not properly validated.
/// Domain names can contain ASCII letters, numbers, dots, and hyphens.
/// Internationalized domain names must be submitted in their ASCII ("xn--") form.
/// </summary>
public class DomainNameValidatorAttribute : ValidationAttribute
{
    // Mirrors the web client's domainNameValidator; keep the two in sync:
    // bitwarden_license/bit-web/src/app/admin-console/organizations/manage/domain-verification/domain-add-edit-dialog/validators/domain-name.validator.ts
    // - Must not start with a URL scheme or "www."
    // - Labels contain ASCII letters, numbers, and hyphens; are 1-63 characters; and cannot start or end with a hyphen
    // - Requires at least one dot; the top-level label is two or more ASCII letters
    private static readonly Regex _domainNameRegex = new(
        @"^(?!(http(s)?:\/\/|www\.))([a-zA-Z0-9]([a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?\.)+[a-zA-Z]{2,}$",
        RegexOptions.Compiled
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
