using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Services.Pam.Api.Models.Response;

namespace Bit.Services.Pam.Rotation.Api.Models.Response;

/// <summary>One daemon's try at executing a rotation job. An element of <see cref="PamRotationJobResponseModel.Attempts"/>.</summary>
public class PamRotationAttemptResponseModel
{
    public PamRotationAttemptResponseModel(PamRotationAttempt attempt)
    {
        ArgumentNullException.ThrowIfNull(attempt);

        Id = attempt.Id;
        JobId = attempt.JobId;
        ClaimedByDaemonId = attempt.ClaimedByDaemonId;
        CipherUpdated = attempt.CipherUpdated;
        Status = attempt.Status;
        FailureReason = attempt.FailureReason;
        SyncState = attempt.SyncState;
        SessionTermination = attempt.SessionTermination;
        CreationDate = attempt.CreationDate.AsUtc();
        ResolvedDate = attempt.ResolvedDate.AsUtc();
    }

    /// <summary>
    /// The attempt's unique identifier.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// The rotation job this attempt was made against.
    /// </summary>
    public Guid JobId { get; }

    /// <summary>
    /// The daemon that executed this attempt, fixed for the attempt's lifetime.
    /// </summary>
    public Guid ClaimedByDaemonId { get; }

    /// <summary>
    /// Whether the daemon wrote the rotated secret back to the cipher.
    /// </summary>
    public bool CipherUpdated { get; }

    /// <summary>
    /// Where the attempt stands -- see <see cref="PamRotationAttemptStatus"/>.
    /// </summary>
    public PamRotationAttemptStatus Status { get; }

    /// <summary>
    /// Why the attempt failed -- the daemon's error code and its optional detail, combined. Null unless
    /// <see cref="Status"/> is <see cref="PamRotationAttemptStatus.Errored"/>.
    /// </summary>
    public string? FailureReason { get; }

    /// <summary>
    /// Whether the failure left the target system's password changed -- see <see cref="PamRotationSyncState"/>.
    /// Null unless the attempt errored.
    /// </summary>
    public PamRotationSyncState? SyncState { get; }

    /// <summary>
    /// The result of the rotation's optional session-termination step -- see
    /// <see cref="PamSessionTerminationOutcome"/>. Null unless the attempt reported success.
    /// </summary>
    public PamSessionTerminationOutcome? SessionTermination { get; }

    /// <summary>
    /// When the attempt was created, at claim time (UTC).
    /// </summary>
    public DateTime CreationDate { get; }

    /// <summary>
    /// When the attempt reached a terminal status (UTC). Null while it is still executing.
    /// </summary>
    public DateTime? ResolvedDate { get; }
}
