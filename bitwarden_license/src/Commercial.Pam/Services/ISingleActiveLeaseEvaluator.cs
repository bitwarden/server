namespace Bit.Commercial.Pam.Services;

public interface ISingleActiveLeaseEvaluator
{
    /// <summary>
    /// Decides whether the per-cipher single-active-lease constraint binds for the caller, using the same
    /// union-over-paths logic as cipher gating. Returns true only when the caller reaches the cipher through at
    /// least one collection and <em>every</em> such collection is governed by a rule whose
    /// <c>SingleActiveLease</c> is true. Any ungated path (no access rule) or a path governed by a non-singleton
    /// rule is an escape that leaves the caller unconstrained, so the method returns false.
    /// </summary>
    Task<bool> AppliesAsync(Guid userId, Guid cipherId);
}
