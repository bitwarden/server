using Bit.Pam.Entities;

namespace Bit.Pam.Models;

/// <summary>
/// A <see cref="PamRotationJob"/> together with every <see cref="PamRotationAttempt"/> recorded against it, oldest
/// first — the read model for a rotation config's attempt-history display (<c>GET configs/{id}</c>), so the caller
/// avoids an N+1 fetching each job's attempts individually.
/// </summary>
public class PamRotationJobDetails : PamRotationJob
{
    public IReadOnlyList<PamRotationAttempt> Attempts { get; set; } = [];

    public static PamRotationJobDetails From(PamRotationJob job, IReadOnlyList<PamRotationAttempt> attempts) => new()
    {
        Id = job.Id,
        RotationConfigId = job.RotationConfigId,
        Source = job.Source,
        Status = job.Status,
        ClaimedByDaemonId = job.ClaimedByDaemonId,
        ClaimedAt = job.ClaimedAt,
        CreationDate = job.CreationDate,
        NextClaimableAt = job.NextClaimableAt,
        ExpiresAt = job.ExpiresAt,
        Attempts = attempts,
    };
}
