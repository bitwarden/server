using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Models;

namespace Bit.Pam.Repositories;

/// <summary>
/// Does not extend <c>IRepository&lt;PamRotationJob, Guid&gt;</c>: every write is a guarded transition, and a generic
/// CRUD surface would invite a check-then-act race around the invariants.
/// </summary>
public interface IPamRotationJobRepository
{
    /// <summary>
    /// Inserts under <c>UPDLOCK, HOLDLOCK</c> (<c>AtMostOneActiveJobPerConfig</c>). The job must already have its id.
    /// </summary>
    Task<PamRotationJobCreateOutcome> CreateGuardedAsync(PamRotationJob job);

    Task<PamRotationJob?> GetByIdAsync(Guid id);

    /// <summary>
    /// First-claim-wins: records the claim and inserts its <see cref="PamRotationAttempt"/> in one transaction, both
    /// stamped with <paramref name="now"/> (<c>AtMostOneInFlightAttemptPerJob</c>). Re-checks
    /// <see cref="PamRotationRules.IsClaimable"/> and the access connector's eligibility.
    /// </summary>
    Task<PamRotationClaimResult> ClaimAsync(Guid jobId, Guid accessConnectorId, DateTime now, TimeSpan releaseDelay);

    /// <summary>The access connector's poll, applying every condition a claim re-checks.</summary>
    Task<ICollection<PamClaimableJob>> GetManyClaimableByAccessConnectorIdAsync(Guid accessConnectorId, DateTime now);

    /// <summary>
    /// Returns every job recorded against the config, each with its attempts oldest first, for the config detail
    /// page's attempt history. Statuses are derived against <paramref name="now"/>.
    /// </summary>
    Task<ICollection<PamRotationJobDetails>> GetManyByConfigIdAsync(Guid configId, DateTime now);

    /// <summary>
    /// The <paramref name="limit"/> most recent jobs this access connector has worked, newest first, for its detail
    /// page. Matches on the attempts, not <see cref="PamRotationJob.ClaimedByAccessConnectorId"/>, since claim fields
    /// clear once a job resolves or is released.
    /// </summary>
    Task<ICollection<PamRotationJobDetails>> GetManyRecentByAccessConnectorIdAsync(Guid accessConnectorId, int limit,
        DateTime now);

    Task<PamRotationAttempt?> GetAttemptByIdAsync(Guid attemptId);

    /// <summary>
    /// Re-verifies the claim, the Executing attempt, and <paramref name="lastKnownRevisionDate"/>, then writes the
    /// cipher. Serializes against the release sweep, so there is no check-then-act window.
    /// </summary>
    Task<PamRotationCipherWriteOutcome> AcceptCipherWriteAsync(Guid attemptId, Guid accessConnectorId,
        string cipherData, DateTime lastKnownRevisionDate, DateTime now);

    /// <summary>
    /// Marks an Executing attempt claimed by <paramref name="accessConnectorId"/> Rotated and its job Succeeded, if
    /// <see cref="PamRotationAttempt.CipherUpdated"/> is set. A guard failure changes nothing and is audited as
    /// <c>report_rejected</c>.
    /// </summary>
    Task<PamRotationAttemptResolveOutcome> MarkAttemptRotatedAsync(Guid attemptId, Guid accessConnectorId,
        PamSessionTerminationOutcome sessionTermination, DateTime now);

    /// <summary>
    /// Marks an Executing attempt claimed by <paramref name="accessConnectorId"/> Errored, then retries the job after
    /// <c>retryBaseDelay * 2^(errored - 1)</c>, or fails it at <paramref name="maxAttempts"/>. A stale report is a
    /// no-op audited as <c>report_rejected</c>.
    /// </summary>
    Task<PamRotationFailureResult> MarkAttemptErroredAsync(Guid attemptId, Guid accessConnectorId,
        string? failureReason, PamRotationSyncState syncState, DateTime now, int maxAttempts, TimeSpan retryBaseDelay);

    /// <summary>
    /// Records each unresolved job past <see cref="PamRotationJob.ExpiresAt"/> in the timeout journal once, and returns
    /// them for audit. Nothing else is written, since TimedOut and Abandoned are derived; the journal row ends the
    /// job's hold on its config.
    /// </summary>
    Task<IReadOnlyList<PamTimedOutJob>> TimeoutDueAsync(DateTime now);

    /// <summary>
    /// Releases claims that have not timed out back to Pending once the heartbeat is stale and the claim lease has
    /// expired, preserving success-wins for a slow but live access connector. A disabled access connector's jobs
    /// release too.
    /// </summary>
    Task<IReadOnlyList<PamReleasedJob>> ReleaseExpiredLeasesAsync(DateTime now, TimeSpan offlineAfter,
        TimeSpan releaseDelay);
}
