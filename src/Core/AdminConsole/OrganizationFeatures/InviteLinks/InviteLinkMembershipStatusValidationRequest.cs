using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

/// <summary>
/// Request for <see cref="Interfaces.IConfirmInviteLinkMembershipStatusValidator"/>.
/// </summary>
public record InviteLinkMembershipStatusValidationRequest
{
    /// <summary>
    /// The organization the user is joining.
    /// </summary>
    public required Organization Organization { get; init; }

    /// <summary>
    /// The user's existing membership in the organization, or <see langword="null"/> if they have none.
    /// </summary>
    public OrganizationUser? ExistingOrganizationUser { get; init; }
}
