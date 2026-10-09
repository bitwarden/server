using Bit.Core.AdminConsole.Utilities.v2.Validation;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;

/// <summary>
/// Validates that the organization's plan can take on the user joining via an invite link.
/// </summary>
/// <remarks>
/// Validates seat availability when joining consumes a seat: a brand-new membership, or promoting a Staged one.
/// The caller supplies the organization.
/// </remarks>
public interface IInviteLinkOrganizationCapabilityValidator
{
    Task<ValidationResult<InviteLinkOrganizationCapabilityValidationRequest>> ValidateAsync(
        InviteLinkOrganizationCapabilityValidationRequest request);
}
