using Bit.Pam.Entities;

namespace Bit.Pam.Models;

/// <summary>
/// A <see cref="PamRotationJob"/> together with the <see cref="PamRotationAttempt"/> rows in scope for the read,
/// oldest first — the read model behind the attempt-history displays, so the caller avoids an N+1 fetching each job's
/// attempts individually. The config detail read (<c>GET configs/{id}</c>) puts every attempt in scope; the daemon
/// detail read (<c>GET daemons/{id}</c>) narrows <see cref="Attempts"/> to the ones that daemon recorded.
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
