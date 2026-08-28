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

    public Guid Id { get; }
    public Guid JobId { get; }
    public Guid ClaimedByDaemonId { get; }
    public bool CipherUpdated { get; }
    public PamRotationAttemptStatus Status { get; }

    /// <summary>Bounded, truncated to 500 characters at write time. Null unless <see cref="Status"/> is Errored.</summary>
    public string? FailureReason { get; }

    public PamRotationSyncState? SyncState { get; }
    public PamSessionTerminationOutcome? SessionTermination { get; }
    public DateTime CreationDate { get; }
    public DateTime? ResolvedDate { get; }
}
