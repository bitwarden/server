namespace Bit.Pam.Enums;

/// <summary>
/// The result of the first-claim-wins <c>PamRotationJob_Claim</c>; each value is the code it returns.
/// </summary>
public enum PamRotationClaimOutcome
{
    /// <summary>
    /// The job is claimed, with an Executing <see cref="Entities.PamRotationAttempt"/> inserted in the same
    /// transaction.
    /// </summary>
    Claimed = 1,

    /// <summary>
    /// The job is not Pending or still in backoff, or its config or target is disabled. Usually another access
    /// connector won the race.
    /// </summary>
    NotClaimable = 0,

    /// <summary>
    /// The job is unknown or outside this access connector's assignments or organization, or the access connector is
    /// disabled. Nothing is persisted.
    /// </summary>
    NotEligible = -1,
}
