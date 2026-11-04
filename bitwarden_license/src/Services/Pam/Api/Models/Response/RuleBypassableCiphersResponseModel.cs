using Bit.HttpExtensions;

namespace Bit.Services.Pam.Api.Models.Response;

/// <summary>
/// Where an access rule fails to gate: the collections letting the ciphers it governs through
/// without a lease.
/// </summary>
public class RuleBypassableCiphersResponseModel : ResponseModel
{
    public RuleBypassableCiphersResponseModel(Guid ruleId, IEnumerable<Guid> ungatedCollectionIds)
        : base("ruleBypassableCiphers")
    {
        ArgumentNullException.ThrowIfNull(ungatedCollectionIds);

        RuleId = ruleId;
        UngatedCollectionIds = ungatedCollectionIds.ToList();
    }

    public Guid RuleId { get; }

    /// <summary>
    /// The collections through which this rule's ciphers are reachable without a lease. Empty when the rule protects
    /// everything it governs, is disabled, or is not in this organization.
    /// </summary>
    public IEnumerable<Guid> UngatedCollectionIds { get; }
}
