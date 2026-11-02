namespace Bit.Services.Pam.Services;

public interface ISingleActiveLeaseEvaluator
{
    /// <summary>
    /// Whether the per-cipher single-active-lease constraint binds for the caller, using the same
    /// union-over-paths logic as cipher gating: true only if the caller reaches the cipher through at least
    /// one collection and <em>every</em> such collection is governed by a rule with <c>SingleActiveLease</c>.
    /// </summary>
    Task<bool> AppliesAsync(Guid userId, Guid cipherId);
}
