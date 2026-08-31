namespace Bit.Pam.Enums;

/// <summary>
/// The result of the atomic first-claim-wins <c>PamRotationJob_Claim</c> update. The stored procedure returns a
/// distinct integer code so <c>ClaimRotationJobCommand</c> can tell a lost race apart from a daemon that was never
/// eligible to claim the job.
/// </summary>
public enum PamRotationClaimOutcome
{
    /// <summary>
    /// The claim succeeded and an Executing <see cref="Entities.PamRotationAttempt"/> was inserted in the same
    /// transaction (stored proc returned 1).
    /// </summary>
    Claimed = 1,

    /// <summary>
    /// The job was not Pending, or its <see cref="Entities.PamRotationJob.NextClaimableAt"/> had not yet arrived,
    /// when the update ran (stored proc returned 0) — another daemon likely won the race.
    /// </summary>
    NotClaimable = 0,

    /// <summary>
    /// The guard <c>EligibleClaimsOnly</c> failed: the config is disabled, the target is not
    /// <see cref="PamTargetSystemStatus.Active"/>, the daemon has no assignment to the target, or the daemon's
    /// organization does not match the config's organization (stored proc returned -1). Nothing was persisted.
    /// </summary>
    NotEligible = -1,
}
