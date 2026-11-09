namespace Bit.Services.Pam.OrganizationFeatures.Queries.Interfaces;

/// <summary>
/// Finds the collections through which an access rule's ciphers are reachable without a lease. A cipher is gated
/// only when every collection it is reachable through gates (see <c>CipherLeaseGate.IsGated</c>).
/// </summary>
public interface IListRuleBypassableCiphersQuery
{
    /// <summary>
    /// Empty when the rule protects everything it governs, or does not exist, belongs to another organization, or is
    /// disabled. Ciphers are not reported, since only the caller's vault can decrypt their names.
    /// </summary>
    Task<ICollection<Guid>> GetUngatedCollectionIdsAsync(Guid organizationId, Guid ruleId);
}
