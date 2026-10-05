namespace Bit.Pam.Enums;

/// <summary>
/// The result of the atomic first-claim-wins <c>PamRotationJob_Claim</c> update: a lost race, vs. an access connector
/// that was never eligible to claim the job.
/// </summary>
public enum PamRotationClaimOutcome
{
    /// <summary>
    /// The claim succeeded and an Executing <see cref="Entities.PamRotationAttempt"/> was inserted in the same
    /// transaction (stored proc returned 1).
    /// </summary>
    Claimed = 1,

    /// <summary>
    /// The job was not Pending, or its <see cref="Entities.PamRotationJob.NextClaimableAt"/> had not arrived
    /// (stored proc returned 0) — another access connector likely won the race.
    /// </summary>
    NotClaimable = 0,

    /// <summary>
    /// The guard <c>EligibleClaimsOnly</c> failed: the config is disabled, the target is not
    /// <see cref="PamTargetSystemStatus.Active"/>, the access connector has no assignment to the target, or the access
    /// connector's organization does not match the config's organization (stored proc returned -1). Nothing was
    /// persisted.
    /// </summary>
    NotEligible = -1,
}
