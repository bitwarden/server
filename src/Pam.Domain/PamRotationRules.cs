using Bit.Pam.Entities;
using Bit.Pam.Enums;

namespace Bit.Pam;

/// <summary>
/// Derived predicates over PAM rotation entities, shared so admin commands, the daemon-facing endpoints, and the
/// sweep jobs cannot drift on a guard's definition.
/// </summary>
public static class PamRotationRules
{
    /// <summary>
    /// Spec <c>DaemonConnection</c>: connected means heartbeated within <paramref name="offlineAfter"/> of
    /// <paramref name="now"/>. A daemon that has never heartbeated is not connected.
    /// </summary>
    public static bool IsConnected(PamDaemon daemon, DateTime now, TimeSpan offlineAfter) =>
        daemon.LastHeartbeatAt is { } lastHeartbeatAt && lastHeartbeatAt >= now - offlineAfter;

    /// <summary>
    /// The "active" job statuses invariant <c>AtMostOneActiveJobPerConfig</c> binds on: a job is active while it is
    /// still claimable or being worked.
    /// </summary>
    public static bool IsActiveJobStatus(PamRotationJobStatus status) =>
        status is PamRotationJobStatus.Pending or PamRotationJobStatus.Claimed;

    /// <summary>
    /// Spec <c>can_offer</c>, minus the has-active-job check — callers combine this with a repository lookup, since
    /// that check needs a query this pure predicate can't make. The config must be enabled, on an
    /// <see cref="PamTargetSystemMethod.Automatic"/> target, and that target must be
    /// <see cref="PamTargetSystemStatus.Active"/>.
    /// </summary>
    public static bool CanOffer(PamRotationConfig config, PamTargetSystemMethod method, PamTargetSystemStatus targetStatus) =>
        config.Enabled && method == PamTargetSystemMethod.Automatic && targetStatus == PamTargetSystemStatus.Active;

    /// <summary>
    /// Spec <c>awaiting_manual_rotation</c>: a manual-target config surfaces an operator obligation on its schedule,
    /// since there is no daemon to offer a job to.
    /// </summary>
    public static bool AwaitingManualRotation(PamRotationConfig config, PamTargetSystemMethod method, DateTime now) =>
        method == PamTargetSystemMethod.Manual && config.Enabled
        && config.NextRotationAt is { } nextRotationAt && nextRotationAt <= now;

    /// <summary>
    /// The point at which the release sweep may reclaim the job from a stale daemon: <see cref="PamRotationJob.ClaimedAt"/>
    /// plus <paramref name="releaseDelay"/>. Null if the job is not claimed.
    /// </summary>
    public static DateTime? ExecuteBy(PamRotationJob job, TimeSpan releaseDelay) =>
        job.ClaimedAt is { } claimedAt ? claimedAt + releaseDelay : null;
}
