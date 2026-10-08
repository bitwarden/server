using Bit.Core.AdminConsole.Enums.Partnerships;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Partnerships;

public record TransitionPartnershipEntitlementRequest
{
    public required Guid OrganizationPartnershipId { get; init; }
    public required string ExternalId { get; init; }
    public required PartnershipEntitlementAction Action { get; init; }
    /// <summary>
    /// The acting Bitwarden user. Required for <see cref="PartnershipEntitlementAction.Activate"/> and
    /// <see cref="PartnershipEntitlementAction.UserExit"/>; never shared with the partner.
    /// </summary>
    public Guid? UserId { get; init; }
    /// <summary>
    /// Partner actions accept only partner-supplied reasons; <see cref="PartnershipEntitlementAction.UserExit"/>
    /// accepts only <see cref="PartnershipEntitlementReason.UserExit"/>; activation accepts none.
    /// </summary>
    public PartnershipEntitlementReason? Reason { get; init; }
    /// <summary>
    /// When the transition took effect at the partner, in UTC. Defaults to now.
    /// </summary>
    public DateTime? EffectiveAt { get; init; }
    /// <summary>
    /// The partner's identifier for the triggering event. Not yet persisted or emitted.
    /// </summary>
    public string? PartnerEventId { get; init; }
}
