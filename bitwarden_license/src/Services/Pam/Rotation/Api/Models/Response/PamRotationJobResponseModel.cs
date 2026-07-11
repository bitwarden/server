using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Services.Pam.Api.Models.Response;

namespace Bit.Services.Pam.Rotation.Api.Models.Response;

/// <summary>
/// One offer of rotation work for a config, together with every attempt recorded against it -- an element of
/// <see cref="PamRotationConfigDetailResponseModel.Jobs"/> (the config detail page's attempt history).
/// </summary>
public class PamRotationJobResponseModel
{
    public PamRotationJobResponseModel(PamRotationJobDetails job)
    {
        ArgumentNullException.ThrowIfNull(job);

        Id = job.Id;
        RotationConfigId = job.RotationConfigId;
        Source = job.Source;
        Status = job.Status;
        ClaimedByDaemonId = job.ClaimedByDaemonId;
        ClaimedAt = job.ClaimedAt.AsUtc();
        CreationDate = job.CreationDate.AsUtc();
        NextClaimableAt = job.NextClaimableAt.AsUtc();
        ExpiresAt = job.ExpiresAt.AsUtc();
        Attempts = job.Attempts.Select(attempt => new PamRotationAttemptResponseModel(attempt)).ToList();
    }

    public Guid Id { get; }
    public Guid RotationConfigId { get; }
    public PamRotationSource Source { get; }
    public PamRotationJobStatus Status { get; }
    public Guid? ClaimedByDaemonId { get; }
    public DateTime? ClaimedAt { get; }
    public DateTime CreationDate { get; }
    public DateTime NextClaimableAt { get; }
    public DateTime ExpiresAt { get; }

    /// <summary>Oldest first.</summary>
    public IReadOnlyList<PamRotationAttemptResponseModel> Attempts { get; }
}
