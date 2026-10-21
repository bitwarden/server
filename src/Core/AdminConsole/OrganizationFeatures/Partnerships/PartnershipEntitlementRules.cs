using Bit.Core.AdminConsole.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Partnerships;

internal static class PartnershipEntitlementRules
{
    /// <summary>
    /// Characters allowed in an external ID besides ASCII letters and digits. All are safe in a URL path segment
    /// without encoding, so every external ID can appear in partner API routes.
    /// </summary>
    public const string ExternalIdPunctuation = "-._~:@";

    /// <remarks>
    /// Also rejects <c>.</c> and <c>..</c>, which URL path normalization removes, and the database field protection
    /// prefix, which the repositories would otherwise mistake for an already-protected value.
    /// </remarks>
    public static bool IsValidExternalId(string? externalId) =>
        !string.IsNullOrEmpty(externalId) &&
        externalId.Length <= OrganizationPartnershipEntitlement.ExternalIdMaxLength &&
        externalId.All(c => char.IsAsciiLetterOrDigit(c) || ExternalIdPunctuation.Contains(c)) &&
        externalId is not ("." or "..") &&
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
