namespace Bit.Services.Pam.OrganizationFeatures.Queries.Interfaces;

/// <summary>
/// Finds where an access rule fails to gate: the collections through which the ciphers it governs are
/// reachable without a lease.
/// </summary>
/// <remarks>
/// A cipher is gated only when <em>every</em> collection it is reachable through gates (see
/// <c>CipherLeaseGate.IsGated</c>), so a credential also sitting in an ordinary collection is not protected.
/// </remarks>
public interface IListRuleBypassableCiphersQuery
{
    /// <summary>
    /// The collections letting <paramref name="ruleId"/>'s ciphers through ungated. Empty means the rule protects
    /// everything it governs, or does not exist, belongs to another organization, or is switched off.
    /// </summary>
    /// <remarks>
    /// Ciphers are not reported: naming them requires decrypting, which only works from the caller's own vault.
    /// </remarks>
    Task<ICollection<Guid>> GetUngatedCollectionIdsAsync(Guid organizationId, Guid ruleId);
}
