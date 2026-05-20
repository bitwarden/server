using System.Net.Mail;

namespace Bit.Core.AdminConsole.Utilities;

public static class InviteLinkDomainValidator
{
    /// <summary>
    /// Returns whether the given email's domain is contained in the list of allowed domains.
    /// </summary>
    public static bool IsEmailDomainAllowed(string? email, IEnumerable<string> allowedDomains)
    {
        if (!MailAddress.TryCreate(email, out var mailAddress))
        {
            return false;
        }

        var emailDomain = mailAddress.Host.ToLowerInvariant();
        var sanitizedDomains = InviteLinkDomainSanitizer.SanitizeDomains(allowedDomains);

        return sanitizedDomains.Count > 0 && sanitizedDomains.Contains(emailDomain);
    }
}
