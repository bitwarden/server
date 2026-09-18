using Bit.HttpExtensions;

namespace Bit.Services.Pam.Api.Models.Response;

/// <summary>
/// Where an access rule fails to gate: the collections letting the ciphers it governs through
/// without a lease.
/// </summary>
/// <remarks>
/// Collection ids only. The affected ciphers are deliberately not reported, since naming one means decrypting
/// it, which only works from the caller's own vault. A non-empty list is itself the warning condition.
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
