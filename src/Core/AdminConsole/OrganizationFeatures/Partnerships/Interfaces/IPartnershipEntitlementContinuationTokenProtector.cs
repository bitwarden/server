namespace Bit.Core.AdminConsole.OrganizationFeatures.Partnerships.Interfaces;

/// <summary>
/// Issues and reads the opaque continuation tokens for listing a partnership's entitlements. A token only reads back
/// for the partnership and filters it was issued for.
/// </summary>
public interface IPartnershipEntitlementContinuationTokenProtector
{
    /// <summary>
    /// Issues a token that resumes <paramref name="request"/>'s listing after <paramref name="after"/>.
    /// </summary>
    string Protect(ListPartnershipEntitlementsRequest request, PartnershipEntitlementKeyset after);

    /// <summary>
    /// Reads <paramref name="request"/>'s continuation token.
    /// </summary>
    /// <returns>False if the token is missing, tampered with, or was issued for another partnership or other
    /// filters.</returns>
    bool TryUnprotect(ListPartnershipEntitlementsRequest request, out PartnershipEntitlementKeyset after);
}
