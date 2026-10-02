using Bit.Pam.Enums;

namespace Bit.Services.Pam.AccessConnector.Rotation.Api.Models.Response;

/// <summary>One access connector's try at executing a rotation job. An element of
/// <see cref="PamRotationJobResponseModel.Attempts"/>.</summary>
public class PamRotationAttemptResponseModel
{
    /// <summary>
    /// The attempt's unique identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The rotation job this attempt was made against.
    /// </summary>
    public Guid JobId { get; set; }

    /// <summary>
    /// The access connector that executed this attempt, fixed for the attempt's lifetime.
    /// </summary>
    public Guid ClaimedByAccessConnectorId { get; set; }

    /// <summary>
    /// Whether the access connector wrote the rotated secret back to the cipher.
    /// </summary>
    public bool CipherUpdated { get; set; }

    /// <summary>
    /// Where the attempt stands -- see <see cref="PamRotationAttemptStatus"/>.
    /// </summary>
    public PamRotationAttemptStatus Status { get; set; }

    /// <summary>
    /// Why the attempt failed -- the access connector's error code and its optional detail, combined. Null unless
    /// <see cref="Status"/> is <see cref="PamRotationAttemptStatus.Errored"/>.
    /// </summary>
    public string? FailureReason { get; set; }

    /// <summary>
    /// Whether the failure left the target system's password changed -- see <see cref="PamRotationSyncState"/>.
    /// Null unless the attempt errored.
    /// </summary>
    public PamRotationSyncState? SyncState { get; set; }

    /// <summary>
    /// The result of the rotation's optional session-termination step -- see
    /// <see cref="PamSessionTerminationOutcome"/>. Null unless the attempt reported success.
    /// </summary>
    public PamSessionTerminationOutcome? SessionTermination { get; set; }

    /// <summary>
    /// When the attempt was created, at claim time (UTC).
    /// </summary>
    public DateTime CreationDate { get; set; }

    /// <summary>
    /// When the attempt reached a terminal status (UTC). Null while it is still executing.
    /// </summary>
    public DateTime? ResolvedDate { get; set; }
}
