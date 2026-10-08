using Bit.Core.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

/// <summary>
/// The data read by <see cref="Interfaces.IInviteLinkPolicyValidator"/>, returned so callers and other validators do
/// not have to read it again.
/// </summary>
public record InviteLinkPolicyValidationResult
{
    /// <summary>
    /// All of the user's organization memberships.
    /// </summary>
    public required ICollection<OrganizationUser> AllOrganizationMemberships { get; init; }

    /// <summary>
    /// Whether the organization's Automatic User Confirmation policy is enforced against the user.
    /// </summary>
    public bool AutoConfirmPolicyEnabled { get; init; }
}
