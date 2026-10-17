using Bit.Core.AdminConsole.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Partnerships;

/// <param name="ContinuationToken">Reads the next page; null on the last page.</param>
public record PartnershipEntitlementPage(
    IReadOnlyList<OrganizationPartnershipEntitlement> Entitlements, string? ContinuationToken);
