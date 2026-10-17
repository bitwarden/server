using Bit.Core.AdminConsole.Enums.Partnerships;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Partnerships;

public record ListPartnershipEntitlementsRequest
{
    public required Guid OrganizationPartnershipId { get; init; }
    public PartnershipEntitlementState? State { get; init; }
    /// <summary>
    /// Only entitlements revised strictly after this UTC instant.
    /// </summary>
    public DateTime? RevisedAfter { get; init; }
    /// <summary>
    /// The token from the previous page, which must have been issued for the same partnership and filters.
    /// </summary>
    public string? ContinuationToken { get; init; }
}
