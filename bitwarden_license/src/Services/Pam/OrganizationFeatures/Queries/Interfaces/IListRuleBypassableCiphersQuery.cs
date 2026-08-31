namespace Bit.Services.Pam.OrganizationFeatures.Queries.Interfaces;

/// <summary>
/// Finds where an access rule fails to gate: the collections through which the ciphers it governs are
/// reachable without a lease.
/// </summary>
/// <remarks>
/// PAM gating is a union: a cipher is withheld only when <em>every</em> collection it can be reached
/// through gates (see <c>CipherLeaseGate.IsGated</c>). A credential sitting in both a governed
/// collection and an ordinary one is therefore not protected at all — whoever can reach the ordinary
/// collection reads it in full, no lease required. That is a real bypass rather than a bug, so the
/// rules admin UI warns about it, and this query is the authoritative answer it warns from.
/// </remarks>
public interface IListRuleBypassableCiphersQuery
{
    /// <summary>
    /// The collections letting <paramref name="ruleId"/>'s ciphers through ungated. Empty means the
    /// rule protects everything it governs — and also that the rule does not exist, belongs to
    /// another organization, or is switched off.
    /// </summary>
    /// <remarks>
    /// Non-empty IS the warning condition: these ids are derived only from ciphers that are actually
    /// bypassable, so there is no separate "is anything wrong" flag to keep in step.
    /// <para>
    /// The ciphers themselves are deliberately NOT reported. Naming them requires decrypting, which
    /// only works from the caller's own vault — and an admin outside the collection, precisely the one
    /// being warned, has none of its ciphers there. The collections are both reliably nameable (the
    /// admin collection read returns every one of them) and what an admin actually acts on, so they
    /// are the whole answer.
    /// </para>
    /// <para>
    /// De-duplicated and reported per rule rather than per cipher, which also bounds the result by the
    /// organization's collection count instead of multiplying out across ciphers.
    /// </para>
    /// </remarks>
    Task<ICollection<Guid>> GetUngatedCollectionIdsAsync(Guid organizationId, Guid ruleId);
}
