using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Services.Pam.Api.Models.Response;

namespace Bit.Services.Pam.AccessConnector.Rotation.Api.Models.Response;

/// <summary>One offer of rotation work for a config, with its attempts.</summary>
public class PamRotationJobResponseModel
{
    public PamRotationJobResponseModel(PamRotationJobDetails job)
    {
        ArgumentNullException.ThrowIfNull(job);

        Id = job.Id;
        RotationConfigId = job.RotationConfigId;
        Source = job.Source;
        Status = job.Status;
        ClaimedByAccessConnectorId = job.ClaimedByAccessConnectorId;
        ClaimedAt = job.ClaimedAt.AsUtc();
        CreationDate = job.CreationDate.AsUtc();
        NextClaimableAt = job.NextClaimableAt.AsUtc();
        ExpiresAt = job.ExpiresAt.AsUtc();
        Attempts = job.Attempts.Select(attempt => new PamRotationAttemptResponseModel(attempt)).ToList();
    }

    public Guid Id { get; set; }

    public Guid RotationConfigId { get; set; }

    public PamRotationSource Source { get; set; }

    /// <summary>A config has at most one pending or claimed job at a time.</summary>
    public PamRotationJobStatus Status { get; set; }

    /// <summary>
    /// The access connector holding the claim. Null unless <see cref="Status"/> is
    /// <see cref="PamRotationJobStatus.Claimed"/>.
    /// </summary>
    public Guid? ClaimedByAccessConnectorId { get; set; }

    /// <summary>
    /// When the current claim was taken. Null unless <see cref="Status"/> is
    /// <see cref="PamRotationJobStatus.Claimed"/>.
    /// </summary>
    public DateTime? ClaimedAt { get; set; }

    public DateTime CreationDate { get; set; }

    /// <summary>The earliest time the job can be claimed; pushed out by retry backoff or a release.</summary>
    public DateTime NextClaimableAt { get; set; }

    /// <summary>When the job times out unless an attempt has succeeded.</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>The job's attempts, oldest first.</summary>
    public IReadOnlyList<PamRotationAttemptResponseModel> Attempts { get; set; } = [];
}
