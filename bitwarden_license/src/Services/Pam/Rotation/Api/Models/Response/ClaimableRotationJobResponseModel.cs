using Bit.HttpExtensions;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Services.Pam.Api.Models.Response;

namespace Bit.Services.Pam.Rotation.Api.Models.Response;

/// <summary>
/// A single claimable rotation job, as a daemon's poll (<c>GET rotation/daemon/jobs</c>) sees it -- the candidate set
/// spec <c>ClaimRotation</c> claims from. <see cref="TargetSystemId"/> is resolved from the job's config, since the
/// job row itself does not carry it (only <c>RotationConfigId</c>).
/// </summary>
public class ClaimableRotationJobResponseModel : ResponseModel
{
    public ClaimableRotationJobResponseModel(PamRotationJob job, Guid targetSystemId)
        : base("pamRotationJob")
    {
        ArgumentNullException.ThrowIfNull(job);

        JobId = job.Id;
        Source = job.Source;
        NextClaimableAt = job.NextClaimableAt.AsUtc();
        ExpiresAt = job.ExpiresAt.AsUtc();
        TargetSystemId = targetSystemId;
    }

    public Guid JobId { get; }
    public PamRotationSource Source { get; }
    public DateTime NextClaimableAt { get; }
    public DateTime ExpiresAt { get; }
    public Guid TargetSystemId { get; }
}
