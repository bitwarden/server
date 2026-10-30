namespace Bit.Services.Pam.Services;

public interface ISingleActiveLeaseEvaluator
{
    /// <summary>
    /// True when the caller reaches the cipher through at least one collection and every such collection has an
    /// enabled rule with <c>SingleActiveLease</c>, as in cipher gating.
    /// </summary>
    Task<bool> AppliesAsync(Guid userId, Guid cipherId);
}
