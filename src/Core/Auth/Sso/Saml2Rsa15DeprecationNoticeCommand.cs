using Bit.Core.Auth.Sso.Mail;
using Bit.Core.Enums;
using Bit.Core.Platform.Mail.Mailer;
using Bit.Core.Repositories;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Auth.Sso;

public class Saml2Rsa15DeprecationNoticeCommand(
    IOrganizationUserRepository organizationUserRepository,
    IMailer mailer,
    ILogger<Saml2Rsa15DeprecationNoticeCommand> logger) : ISaml2Rsa15DeprecationNoticeCommand
{
    public async Task SendAsync(Guid organizationId)
    {
        try
        {
            var admins = await organizationUserRepository.GetManyByMinimumRoleAsync(organizationId, OrganizationUserType.Admin);

            var emails = admins
                .Select(a => a.Email)
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (emails.Count == 0)
            {
                return;
            }

            await mailer.SendEmail(new Saml2Rsa15DeprecationMail
            {
                ToEmails = emails,
                View = new Saml2Rsa15DeprecationMailView()
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send the RSA 1.5 deprecation email.");
        }
    }
}
