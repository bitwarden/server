using Bit.Core.AdminConsole.Utilities.v2.Validation;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;

/// <summary>
/// Validates the one-admin-per-Free-organization plan limit for a user joining via an invite link.
/// </summary>
/// <remarks>
/// Only applies when the user will be an Owner or Admin of a Free organization, which happens when an email
/// invitation carrying that role is accepted or confirmed through the link. A brand-new member always joins as a User.
/// </remarks>
public interface IInviteLinkFreeOrganizationAdminValidator
{
    Task<ValidationResult<InviteLinkFreeOrganizationAdminValidationRequest>> ValidateAsync(
        InviteLinkFreeOrganizationAdminValidationRequest request);
}
