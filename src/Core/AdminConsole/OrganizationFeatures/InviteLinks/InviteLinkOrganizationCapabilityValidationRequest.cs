using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

/// <summary>
/// Request for <see cref="Interfaces.IInviteLinkOrganizationCapabilityValidator"/>.
/// </summary>
public record InviteLinkOrganizationCapabilityValidationRequest
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
    /// The user's existing membership in the organization, or <see langword="null"/> when joining would create a
    /// brand-new membership. A brand-new or Staged membership needs an available seat.
    /// </summary>
    public OrganizationUser? ExistingOrganizationUser { get; init; }
}
