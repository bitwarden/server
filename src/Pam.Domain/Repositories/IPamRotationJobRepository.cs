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
    /// Guarded insert-if-no-active-job for the job's config, under <c>UPDLOCK, HOLDLOCK</c> (invariant
    /// <c>AtMostOneActiveJobPerConfig</c>; spec <c>OfferRotation</c>'s single creation point). The job must already
    /// have its id assigned.
    /// </summary>
    Task<PamRotationJobCreateOutcome> CreateGuardedAsync(PamRotationJob job);

    Task<PamRotationJob?> GetByIdAsync(Guid id);

    /// <summary>
    /// Atomic first-claim-wins update: flips the job Pending → Claimed and inserts its Executing
    /// <see cref="PamRotationAttempt"/> in the same transaction (invariant <c>AtMostOneInFlightAttemptPerJob</c>).
    /// Re-checks <c>EligibleClaimsOnly</c> (config enabled, target active, the daemon is assigned to the target, and
    /// the daemon's organization matches the config's) before claiming. On success the result carries the work
    /// snapshot (spec <c>ClaimRotation</c>), including <c>ExecuteBy = now + releaseDelay</c>.
    /// </summary>
    Task<PamRotationClaimResult> ClaimAsync(Guid jobId, Guid daemonId, DateTime now, TimeSpan releaseDelay);

    /// <summary>Returns the daemon's currently claimable jobs — jobs on targets it is assigned to that are Pending and past <c>NextClaimableAt</c>. The daemon's poll.</summary>
    Task<ICollection<PamRotationJob>> GetManyClaimableByDaemonIdAsync(Guid daemonId, DateTime now);

    /// <summary>Returns every job recorded against the config, each with its attempts, oldest first — the config detail page's attempt history.</summary>
    Task<ICollection<PamRotationJobDetails>> GetManyByConfigIdAsync(Guid configId);

    Task<PamRotationAttempt?> GetAttemptByIdAsync(Guid attemptId);

    /// <summary>
    /// Atomic write-capability check and write: under one lock, re-verifies the job is Claimed by
    /// <paramref name="daemonId"/>, the attempt is Executing, and <paramref name="lastKnownRevisionDate"/> still
    /// matches the cipher's current revision date, then replaces the cipher's <c>Data</c>, bumps its revision date,
    /// and sets <see cref="PamRotationAttempt.CipherUpdated"/>. Serializes against the release/timeout sweeps so
    /// there is no check-then-act window between them and this write (spec <c>AcceptCipherUpdate</c> /
    /// <c>RejectCipherUpdate</c>, plus the revision-date guard added to protect concurrent user edits).
    /// </summary>
    Task<PamRotationCipherWriteOutcome> AcceptCipherWriteAsync(Guid attemptId, Guid daemonId, string cipherData,
        DateTime lastKnownRevisionDate, DateTime now);

    /// <summary>
    /// Resolves a successful attempt (guards: Executing ∧ claimed by <paramref name="daemonId"/> ∧
    /// <see cref="PamRotationAttempt.CipherUpdated"/> — the <c>VerifiedBeforeSuccess</c> backstop). On success also
    /// flips the job to Succeeded and clears its claim fields. Guard failure is a stale report (spec
    /// <c>RejectStaleSuccess</c>) — nothing changes, audit it as <c>report_rejected</c>.
    /// </summary>
    Task<PamRotationAttemptResolveOutcome> MarkAttemptRotatedAsync(Guid attemptId, Guid daemonId,
        PamSessionTerminationOutcome sessionTermination, DateTime now);

    /// <summary>
    /// Resolves a failed attempt (guards: Executing ∧ claimed by <paramref name="daemonId"/>). On success, marks the
    /// attempt Errored with the (already truncated) <paramref name="failureReason"/> and <paramref name="syncState"/>,
    /// then either retries the job — back to Pending, claim fields cleared,
    /// <c>NextClaimableAt = now + retryBaseDelay·2^(erroredCount−1)</c> — when the errored-attempt count is under
    /// <paramref name="maxAttempts"/>, or fails it outright once the budget is exhausted. Guard failure is a stale
    /// report (spec <c>RejectStaleFailureReport</c>) — nothing changes, audit it as <c>report_rejected</c>.
    /// </summary>
    Task<PamRotationFailureResult> MarkAttemptErroredAsync(Guid attemptId, Guid daemonId, string? failureReason,
        PamRotationSyncState syncState, DateTime now, int maxAttempts, TimeSpan retryBaseDelay);

    /// <summary>
    /// Set-based sweep (spec <c>JobTimesOut</c>): moves every job still Pending or Claimed past
    /// <see cref="PamRotationJob.ExpiresAt"/> with no Rotated attempt to <see cref="PamRotationJobStatus.TimedOut"/>
    /// (clearing claim fields) and abandons any Executing attempt against it. Returns one row per timed-out job for
    /// audit emission — <see cref="PamTimedOutJob.AttemptCount"/> distinguishes unroutable (never claimed) from
    /// stuck (claimed at least once).
    /// </summary>
    Task<IReadOnlyList<PamTimedOutJob>> TimeoutDueAsync(DateTime now);

    /// <summary>
    /// Set-based sweep: releases claimed jobs back to Pending when their daemon's heartbeat has gone stale
    /// (<paramref name="offlineAfter"/>) AND their claim lease has expired
    /// (<c>now &gt;= ClaimedAt + releaseDelay</c>) AND no Rotated attempt exists — releasing only at lease expiry,
    /// not at stale detection, preserves success-wins for a slow-but-live daemon. <c>NextClaimableAt</c> is set to
    /// the pre-clear <c>ClaimedAt + releaseDelay</c> in the same update; claim fields are cleared and the Executing
    /// attempt is abandoned (budget not charged). Keys on heartbeat staleness only, never on daemon status, so a
    /// revoked daemon's jobs release too. Returns one row per released job for audit emission.
    /// </summary>
    Task<IReadOnlyList<PamReleasedJob>> ReleaseExpiredLeasesAsync(DateTime now, TimeSpan offlineAfter,
        TimeSpan releaseDelay);
}
