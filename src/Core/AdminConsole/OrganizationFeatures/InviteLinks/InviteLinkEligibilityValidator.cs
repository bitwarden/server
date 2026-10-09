using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;
using Bit.Core.AdminConsole.Utilities;
using Bit.Core.AdminConsole.Utilities.v2.Validation;
using static Bit.Core.AdminConsole.Utilities.v2.Validation.ValidationResultHelpers;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

/// <summary>
/// See <see cref="IInviteLinkEligibilityValidator"/>.
/// </summary>
public class InviteLinkEligibilityValidator : IInviteLinkEligibilityValidator
{
    public ValidationResult<InviteLinkEligibilityValidationRequest> Validate(InviteLinkEligibilityValidationRequest request)
    {
        var link = request.InviteLink;
        var organization = request.Organization;
        var user = request.User;

        if (link is null || !link.CodeMatches(request.Code.ToString()))
        {
            return Invalid(request, new InviteLinkNotFound());
        }

        if (organization is null or { Enabled: false })
        {
            return Invalid(request, new InviteLinkNotFound());
        }

        if (!organization.UseInviteLinks)
        {
            return Invalid(request, new InviteLinkNotAvailable());
        }

        // The domain is checked before email verification so a user on a disallowed domain is told so first,
        // rather than being sent through the client's email verification step only to be rejected afterwards.
        // Auth's invite link email verification flow (PM-42435) relies on this order; coordinate with Auth before
        // changing it.
        if (!InviteLinkDomainValidator.IsEmailDomainAllowed(user.Email, link.GetAllowedDomains()))
        {
            return Invalid(request, new EmailDomainNotAllowed(organization.DisplayName()));
        }

        // Security: allowed domains only provide protection if the user has proven control over their email address.
        if (!user.EmailVerified)
        {
            return Invalid(request, new EmailNotVerified());
        }

        return Valid(request);
    }
}
