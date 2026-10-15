using Bit.Core.AdminConsole.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Partnerships;

internal static class PartnershipEntitlementRules
{
    /// <remarks>
    /// Rejects the database field protection prefix, which the repositories would otherwise mistake for an
    /// already-protected value.
    /// </remarks>
    public static bool IsValidExternalId(string? externalId) =>
        !string.IsNullOrWhiteSpace(externalId) &&
        externalId.Length <= OrganizationPartnershipEntitlement.ExternalIdMaxLength &&
        !externalId.StartsWith(Constants.DatabaseFieldProtectedPrefix, StringComparison.Ordinal);

    /// <summary>
    /// Only strictly older transitions are stale; one at the same instant as the last applied transition is applied.
    /// </summary>
    public static bool IsStale(OrganizationPartnershipEntitlement entitlement, DateTime effectiveAt) =>
        effectiveAt < entitlement.LastAppliedEffectiveDate;

    /// <summary>
    /// A revision date later than <paramref name="readRevisionDate"/>, as a conditional replace requires, even when
    /// <paramref name="now"/> trails it from request overlap or clock skew. The 1 ms step survives every provider's precision.
    /// </summary>
    public static DateTime NextRevisionDate(DateTime readRevisionDate, DateTime now) =>
        now > readRevisionDate ? now : readRevisionDate.AddMilliseconds(1);
}
