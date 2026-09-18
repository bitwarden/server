namespace Bit.Services.Pam.OrganizationFeatures.Queries.Interfaces;

/// <summary>
/// Finds where an access rule fails to gate: the collections through which the ciphers it governs are
/// reachable without a lease.
/// </summary>
/// <remarks>
/// PAM gating is withheld only when <em>every</em> collection a cipher is reachable through gates (see
/// <c>CipherLeaseGate.IsGated</c>), so a credential also sitting in an ordinary collection is not protected at
/// all. This is a real bypass rather than a bug, and this query is the rules admin UI's authoritative warning.
/// </remarks>
public interface IListRuleBypassableCiphersQuery
{
    /// <summary>
    /// The collections letting <paramref name="ruleId"/>'s ciphers through ungated. Empty means the rule protects
    /// everything it governs, or does not exist, belongs to another organization, or is switched off.
    /// </summary>
    /// <remarks>
    /// The ciphers themselves are deliberately not reported: naming them requires decrypting, which only works
    /// from the caller's own vault, and an admin outside the collection has none of its ciphers there.
    /// </remarks>
    Task<ICollection<Guid>> GetUngatedCollectionIdsAsync(Guid organizationId, Guid ruleId);
}
