using Bit.HttpExtensions;

namespace Bit.Services.Pam.Api.Models.Response;

/// <summary>
/// Where an access rule fails to gate: the collections letting the ciphers it governs through
/// without a lease.
/// </summary>
/// <remarks>
/// Collection ids only. The affected ciphers are deliberately not reported — naming one means
/// decrypting it, which only works from the caller's own vault, and an admin outside the collection
/// (precisely the one being warned) has none of its ciphers there. The collections are both reliably
/// nameable by any admin and what remediation actually acts on.
/// <para>
/// A non-empty list IS the warning condition, so there is no separate flag to keep in step. The list
/// is de-duplicated and bounded by the organization's collection count.
/// </para>
/// </remarks>
public class RuleBypassableCiphersResponseModel : ResponseModel
{
    public RuleBypassableCiphersResponseModel(Guid ruleId, IEnumerable<Guid> ungatedCollectionIds)
        : base("ruleBypassableCiphers")
    {
        ArgumentNullException.ThrowIfNull(ungatedCollectionIds);

        RuleId = ruleId;
        UngatedCollectionIds = ungatedCollectionIds.ToList();
    }

    /// <summary>
    /// The rule these collections were assessed against.
    /// </summary>
    public Guid RuleId { get; }

    /// <summary>
    /// The collections through which this rule's ciphers are reachable without a lease — the gaps an
    /// admin closes to fix this. Empty means the rule protects everything it governs, which is the
    /// normal answer and the one that shows no warning.
    /// </summary>
    public IEnumerable<Guid> UngatedCollectionIds { get; }
}
