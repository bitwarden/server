using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Services.Pam.Api.Models.Response;

namespace Bit.Services.Pam.AccessConnector.Rotation.Api.Models.Response;

/// <summary>One access connector's try at executing a rotation job.</summary>
public class PamRotationAttemptResponseModel
{
    public PamRotationAttemptResponseModel(PamRotationAttemptDetails attempt)
    {
        ArgumentNullException.ThrowIfNull(attempt);

        Id = attempt.Id;
        JobId = attempt.JobId;
        ClaimedByAccessConnectorId = attempt.ClaimedByAccessConnectorId;
        CipherUpdated = attempt.CipherUpdated;
        Status = attempt.Status;
        FailureReason = attempt.FailureReason;
        SyncState = attempt.SyncState;
        SessionTermination = attempt.SessionTermination;
        CreationDate = attempt.CreationDate.AsUtc();
        ResolvedDate = attempt.ResolvedDate.AsUtc();
    }

    public Guid Id { get; set; }

    public Guid JobId { get; set; }

    /// <summary>The access connector that executed this attempt, fixed for its lifetime.</summary>
    public Guid ClaimedByAccessConnectorId { get; set; }

    public bool CipherUpdated { get; set; }

    public PamRotationAttemptStatus Status { get; set; }

    /// <summary>
    /// The access connector's error code and optional detail, combined. Null unless <see cref="Status"/> is
    /// <see cref="PamRotationAttemptStatus.Errored"/>.
    /// </summary>
    public string? FailureReason { get; set; }

    /// <summary>
    /// Whether the failure left the target system's password changed. Null unless the attempt errored.
    /// </summary>
    public PamRotationSyncState? SyncState { get; set; }

    /// <summary>The outcome of the optional session-termination step. Null unless the attempt succeeded.</summary>
    public PamSessionTerminationOutcome? SessionTermination { get; set; }

    /// <summary>When the attempt was created, at claim time.</summary>
    public DateTime CreationDate { get; set; }

    /// <summary>When the attempt reached a terminal status. Null while it is still executing.</summary>
    public DateTime? ResolvedDate { get; set; }
}
