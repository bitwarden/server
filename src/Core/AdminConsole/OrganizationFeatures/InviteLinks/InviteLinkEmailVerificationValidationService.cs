using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;
using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.Entities;
using Bit.Core.Settings;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

/// <inheritdoc />
public class InviteLinkEmailVerificationValidationService(IGlobalSettings globalSettings) : IInviteLinkEmailVerificationValidationService
{
    public Error? ValidateEmailVerified(User user)
    {
        // Security: allowed domains only provide protection if the user has proven control over their email address.
        // Self-hosted instances may opt out, but cloud always requires verification.
        var requireEmailVerification = !globalSettings.SelfHosted || globalSettings.EnableOpenInviteLinkEmailVerification;
        if (!user.EmailVerified && requireEmailVerification)
        {
            return new EmailNotVerified();
        }

        return null;
    }
}
