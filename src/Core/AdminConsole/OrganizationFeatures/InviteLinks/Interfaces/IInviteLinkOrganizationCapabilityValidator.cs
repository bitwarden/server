using Bit.Core.AdminConsole.Utilities.v2.Validation;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;

/// <summary>
/// Validates that the organization's plan can take on the user joining via an invite link.
/// </summary>
/// <remarks>
/// The following are validated, in this order:
/// <list type="bullet">
///     <item>The one-admin-per-Free-organization limit, for an existing Owner or Admin membership.</item>
///     <item>Seat availability, only when joining creates a brand-new membership (no existing organization user).</item>
/// </list>
/// The caller supplies the organization.
/// </remarks>
public interface IInviteLinkOrganizationCapabilityValidator
{
    Task<ValidationResult<InviteLinkOrganizationCapabilityValidationRequest>> ValidateAsync(
        InviteLinkOrganizationCapabilityValidationRequest request);
}
