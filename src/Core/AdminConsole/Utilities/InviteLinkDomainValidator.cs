using Bit.Core.Utilities;

namespace Bit.Core.AdminConsole.Utilities;

public static class InviteLinkDomainValidator
{
    /// <summary>
    /// Returns whether the given email's domain is contained in the list of allowed domains.
    /// </summary>
    public static bool IsEmailDomainAllowed(string? email, IEnumerable<string> allowedDomains)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.IsValidEmail())
        {
            return false;
        }

        var emailDomain = EmailValidation.GetDomain(email);
        var sanitizedDomains = InviteLinkDomainSanitizer.SanitizeDomains(allowedDomains);

        return sanitizedDomains.Count > 0 && sanitizedDomains.Contains(emailDomain);
    }
}
