using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;
using Bit.Core.AdminConsole.Utilities.v2.Validation;
using Bit.Core.Billing.Enums;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using static Bit.Core.AdminConsole.Utilities.v2.Validation.ValidationResultHelpers;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

/// <summary>
/// See <see cref="IInviteLinkFreeOrganizationAdminValidator"/>.
/// </summary>
public class InviteLinkFreeOrganizationAdminValidator(IOrganizationUserRepository organizationUserRepository)
    : IInviteLinkFreeOrganizationAdminValidator
{
    public async Task<ValidationResult<InviteLinkFreeOrganizationAdminValidationRequest>> ValidateAsync(
        InviteLinkFreeOrganizationAdminValidationRequest request)
    {
        // An email invite can carry an Admin/Owner role, so enforce the "one admin of a Free org" rule (a plan
        // constraint, not a policy), mirroring AcceptOrgUserCommand.
        if (request.ExistingOrganizationUser?.Type is OrganizationUserType.Owner or OrganizationUserType.Admin
            && request.Organization.PlanType == PlanType.Free
            && await organizationUserRepository.GetCountByFreeOrganizationAdminUserAsync(request.User.Id) > 0)
        {
            return Invalid(request, new OnlyOneFreeOrganizationAdminAllowed());
        }

        return Valid(request);
    }
}
