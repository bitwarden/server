using Bit.Pam.Entities;
using Bit.Pam.Enums;

namespace Bit.Pam;

/// <summary>
/// Rotation predicates shared by the admin commands, the connector-facing endpoints, and the sweeps, so each guard has
/// one definition.
/// </summary>
public static class PamRotationRules
{
    public static bool IsConnected(PamAccessConnector accessConnector, DateTime now, TimeSpan offlineAfter) =>
        accessConnector.LastHeartbeatAt is { } lastHeartbeatAt && lastHeartbeatAt >= now - offlineAfter;

    /// <summary>
    /// The active statuses. <c>AtMostOneActiveJobPerConfig</c> also counts a timed-out job until the sweep records it.
    /// </summary>
    public static bool IsActiveJobStatus(PamRotationJobStatus status) =>
        status is PamRotationJobStatus.Pending or PamRotationJobStatus.Claimed;

    /// <summary>Whether a job may be offered, minus the active-job check, which needs a repository query.</summary>
    public static bool CanOffer(PamRotationConfig config, PamTargetSystemMethod method, PamTargetSystemStatus targetStatus) =>
        config.Enabled && method == PamTargetSystemMethod.Automatic && targetStatus == PamTargetSystemStatus.Active;

    /// <summary>
    /// Spec <c>is_claimable</c>: pending, past its backoff, on a config that is still live. Pausing the config or
    /// disabling its target holds a pending job without changing its status. The claim and poll queries mirror it.
    /// </summary>
    public static bool IsClaimable(
        PamRotationJob job, PamRotationConfig config, PamTargetSystemStatus targetStatus, DateTime now) =>
        job.Action == PamRotationJobAction.None && now < job.ExpiresAt && job.NextClaimableAt <= now
        && config.Enabled && targetStatus == PamTargetSystemStatus.Active;

    /// <summary>
    /// Whether <paramref name="attempt"/> was created by the claim <paramref name="job"/> records, whose
    /// <see cref="PamRotationJob.ClaimedAt"/> equals the attempt's <see cref="PamRotationAttempt.CreationDate"/>. A
    /// claim that timed out still matches, so callers pair this with the job's derived status.
    /// </summary>
    public static bool IsCurrentAttempt(PamRotationJob job, PamRotationAttempt attempt) =>
        attempt.JobId == job.Id && job.ClaimedAt == attempt.CreationDate
        && job.ClaimedByAccessConnectorId == attempt.ClaimedByAccessConnectorId;

    /// <summary>
    /// Spec <c>awaiting_manual_rotation</c>: a manual-target config surfaces an operator obligation on its schedule,
    /// since there is no access connector to offer a job to.
    /// </summary>
    public static bool AwaitingManualRotation(PamRotationConfig config, PamTargetSystemMethod method, DateTime now) =>
        method == PamTargetSystemMethod.Manual && config.Enabled
        && config.NextRotationAt is { } nextRotationAt && nextRotationAt <= now;

    /// <summary>When the release sweep may reclaim the job from a stale access connector; null if unclaimed.</summary>
    public static DateTime? ExecuteBy(PamRotationJob job, TimeSpan releaseDelay) =>
        job.ClaimedAt is { } claimedAt ? claimedAt + releaseDelay : null;
}
