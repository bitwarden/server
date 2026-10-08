namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

/// <summary>
/// The data read by <see cref="Interfaces.IInviteLinkOrganizationCapabilityValidator"/>, returned so callers and
/// other validators do not have to read it again.
/// </summary>
public record InviteLinkOrganizationCapabilityValidationResult
{
    /// <summary>
    /// The organization's occupied Password Manager seat count, or <see langword="null"/> when seats were not checked
    /// because joining does not create a new membership.
    /// </summary>
    public int? OccupiedSeatCount { get; init; }
}
