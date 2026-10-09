using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

/// <summary>
/// Request for <see cref="Interfaces.IInviteLinkFreeOrganizationAdminValidator"/>.
/// </summary>
public record InviteLinkFreeOrganizationAdminValidationRequest
{
    /// <summary>
    /// The organization the user is joining.
    /// </summary>
    public required Organization Organization { get; init; }

    /// <summary>
    /// The user joining the organization.
    /// </summary>
    public required User User { get; init; }

    /// <summary>
    /// The user's existing membership in the organization, if any. Its role is the role the user will hold;
    /// a brand-new member joins as <see cref="Bit.Core.Enums.OrganizationUserType.User"/>.
    /// </summary>
    public OrganizationUser? ExistingOrganizationUser { get; init; }
}
