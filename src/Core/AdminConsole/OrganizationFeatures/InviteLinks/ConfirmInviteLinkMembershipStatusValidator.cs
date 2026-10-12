using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;
using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.AdminConsole.Utilities.v2.Validation;
using Bit.Core.Enums;
using static Bit.Core.AdminConsole.Utilities.v2.Validation.ValidationResultHelpers;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

/// <summary>
/// See <see cref="IConfirmInviteLinkMembershipStatusValidator"/>.
/// </summary>
public class ConfirmInviteLinkMembershipStatusValidator : IConfirmInviteLinkMembershipStatusValidator
{
    public ValidationResult<InviteLinkMembershipStatusValidationRequest> Validate(
        InviteLinkMembershipStatusValidationRequest request)
    {
        var orgName = request.Organization.DisplayName();

        Error? error = request.ExistingOrganizationUser switch
        {
            { RevocationReason: not null } => new OrganizationAccessRevoked(orgName),
            { Status: OrganizationUserStatusType.Confirmed } => new AlreadyOrganizationMember(orgName),
            _ => null
        };

        return error is null ? Valid(request) : Invalid(request, error);
    }
}
