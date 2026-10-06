using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Models;

namespace Bit.Pam.Repositories;

/// <summary>
/// Repository for <see cref="PamRotationJob"/> and its <see cref="PamRotationAttempt"/> children. Deliberately does
/// not extend <c>IRepository&lt;PamRotationJob, Guid&gt;</c>: every write is a guarded transition (creation, claim,
/// cipher write, resolution, sweep), never a plain insert/replace, so a generic CRUD surface would invite a
/// check-then-act race around the invariants below.
/// </summary>
public interface IPamRotationJobRepository
{
    /// <summary>
    /// Guarded insert for the job's config, under <c>UPDLOCK, HOLDLOCK</c> (invariant
    /// <c>AtMostOneActiveJobPerConfig</c>). The job must already have its id assigned.
    /// </summary>
    Task<PamRotationJobCreateOutcome> CreateGuardedAsync(PamRotationJob job);

    Task<PamRotationJob?> GetByIdAsync(Guid id);

    /// <summary>
    /// Atomic first-claim-wins update: records the claim and inserts its <see cref="PamRotationAttempt"/> in the same
    /// transaction, both stamped with <paramref name="now"/> (invariant <c>AtMostOneInFlightAttemptPerJob</c>).
    /// Re-checks <see cref="PamRotationRules.IsClaimable"/> and the access connector's eligibility before claiming.
    /// </summary>
    Task<PamRotationClaimResult> ClaimAsync(Guid jobId, Guid accessConnectorId, DateTime now, TimeSpan releaseDelay);

    /// <summary>
    /// Returns the access connector's currently claimable jobs — jobs on targets it is assigned to that are Pending and
    /// past <c>NextClaimableAt</c>. The access connector's poll. Each row carries its config's target system id,
    /// projected from the join the eligibility check already makes, so the caller does not re-read the config per job.
    /// </summary>
    Task<ICollection<PamClaimableJob>> GetManyClaimableByAccessConnectorIdAsync(Guid accessConnectorId, DateTime now);

    /// <summary>
    /// Returns every job recorded against the config, each with its attempts, oldest first — the config detail page's
    /// attempt history. Statuses are derived against <paramref name="now"/>.
    /// </summary>
    Task<ICollection<PamRotationJobDetails>> GetManyByConfigIdAsync(Guid configId, DateTime now);

    /// <summary>
    /// Returns the <paramref name="limit"/> most recent jobs this access connector has worked, newest first — the
    /// access connector detail page's recent activity. Matches on the attempts, not
    /// <see cref="PamRotationJob.ClaimedByAccessConnectorId"/>, since a job's claim fields are cleared once it
    /// resolves or is released. Statuses are derived against <paramref name="now"/>.
    /// </summary>
    Task<ICollection<PamRotationJobDetails>> GetManyRecentByAccessConnectorIdAsync(Guid accessConnectorId, int limit,
        DateTime now);

    Task<PamRotationAttempt?> GetAttemptByIdAsync(Guid attemptId);

    /// <summary>
    /// Atomic write-capability check and write: re-verifies the job is Claimed by <paramref name="accessConnectorId"/>,
    /// the attempt is Executing, and <paramref name="lastKnownRevisionDate"/> still matches the cipher's current
    /// revision date, then writes the cipher's data. Serializes against the release/timeout sweeps so there is no
    /// check-then-act window between them and this write.
    /// </summary>
    Task<PamRotationCipherWriteOutcome> AcceptCipherWriteAsync(Guid attemptId, Guid accessConnectorId,
        string cipherData, DateTime lastKnownRevisionDate, DateTime now);

    /// <summary>
    /// Resolves a successful attempt (guards: Executing ∧ claimed by <paramref name="accessConnectorId"/> ∧
    /// <see cref="PamRotationAttempt.CipherUpdated"/> — the <c>VerifiedBeforeSuccess</c> backstop). On success also
    /// flips the job to Succeeded and clears its claim fields. Guard failure is a stale report (spec
    /// <c>RejectStaleSuccess</c>) — nothing changes, audit it as <c>report_rejected</c>.
    /// </summary>
    Task<PamRotationAttemptResolveOutcome> MarkAttemptRotatedAsync(Guid attemptId, Guid accessConnectorId,
        PamSessionTerminationOutcome sessionTermination, DateTime now);

    /// <summary>
    /// Resolves a failed attempt (guards: Executing, claimed by <paramref name="accessConnectorId"/>). On success,
    /// retries the job with <c>NextClaimableAt = now + retryBaseDelay·2^(erroredCount−1)</c> if under
    /// <paramref name="maxAttempts"/>, otherwise fails it outright. A stale report is a no-op audited as
    /// <c>report_rejected</c>.
    /// </summary>
    Task<PamRotationFailureResult> MarkAttemptErroredAsync(Guid attemptId, Guid accessConnectorId,
        string? failureReason, PamRotationSyncState syncState, DateTime now, int maxAttempts, TimeSpan retryBaseDelay);

    /// <summary>
    /// Set-based sweep: records each unresolved job past <see cref="PamRotationJob.ExpiresAt"/> in the timeout journal,
    /// once. Writes nothing to the job or its attempts, whose TimedOut and Abandoned are derived; the journal row is
    /// what releases the job's hold on its config. Returns one row per newly recorded job for audit emission.
    /// </summary>
    Task<IReadOnlyList<PamTimedOutJob>> TimeoutDueAsync(DateTime now);

    /// <summary>
    /// Set-based sweep: releases claims that have not timed out back to Pending once the access connector's heartbeat
    /// is stale and the claim lease has expired, preserving success-wins for a slow-but-live access connector. Keys on
    /// heartbeat staleness only, never access connector status, so a revoked access connector's jobs release too.
    /// Returns one row per released job for audit emission.
    /// </summary>
    Task<IReadOnlyList<PamReleasedJob>> ReleaseExpiredLeasesAsync(DateTime now, TimeSpan offlineAfter,
        TimeSpan releaseDelay);
}
