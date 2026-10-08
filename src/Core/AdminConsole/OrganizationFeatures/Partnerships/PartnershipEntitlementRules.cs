using Bit.Core.AdminConsole.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Partnerships;

internal static class PartnershipEntitlementRules
{
    public static bool IsValidExternalId(string? externalId) =>
        !string.IsNullOrWhiteSpace(externalId) &&
        externalId.Length <= OrganizationPartnershipEntitlement.ExternalIdMaxLength;

    /// <summary>
    /// Only strictly older transitions are stale; one at the same instant as the last applied transition is applied.
    /// </summary>
    public static bool IsStale(OrganizationPartnershipEntitlement entitlement, DateTime effectiveAt) =>
        effectiveAt < entitlement.LastAppliedEffectiveDate;
}
