using Bit.HttpExtensions;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Services.Pam.Api.Models.Response;

namespace Bit.Services.Pam.Rotation.Api.Models.Response;

/// <summary>
/// A single claimable rotation job, as a daemon's poll (<c>GET rotation/daemon/jobs</c>) sees it -- the candidate set
/// spec <c>ClaimRotation</c> claims from. <see cref="TargetSystemId"/> comes from the config the poll query already
/// joins for its eligibility check, since the job row itself carries only <c>RotationConfigId</c>.
/// </summary>
public class ClaimableRotationJobResponseModel : ResponseModel
{
    public ClaimableRotationJobResponseModel(PamClaimableJob job)
        : base("pamRotationJob")
    {
        ArgumentNullException.ThrowIfNull(job);

        JobId = job.Id;
        Source = job.Source;
        NextClaimableAt = job.NextClaimableAt.AsUtc();
        ExpiresAt = job.ExpiresAt.AsUtc();
        TargetSystemId = job.TargetSystemId;
    }

    public Guid JobId { get; }
    public PamRotationSource Source { get; }
    public DateTime NextClaimableAt { get; }
    public DateTime ExpiresAt { get; }
    public Guid TargetSystemId { get; }
}
