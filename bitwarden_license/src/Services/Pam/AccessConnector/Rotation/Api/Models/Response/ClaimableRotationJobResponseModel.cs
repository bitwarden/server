using  Bit.HttpExtensions;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Services.Pam.Api.Models.Response;

namespace Bit.Services.Pam.AccessConnector.Rotation.Api.Models.Response;

/// <summary>
/// A single claimable rotation job, as an access connector's poll (<c>GET access-connectors/rotation/jobs</c>) sees it --
/// the candidate set spec <c>ClaimRotation</c> claims from. <see cref="TargetSystemId"/> comes from the config the poll
/// query already joins for its eligibility check, since the job row itself carries only <c>RotationConfigId</c>.
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

    /// <summary>
    /// The rotation job's unique identifier -- the id a claim is placed against.
    /// </summary>
    public Guid JobId { get; set; }

    /// <summary>
    /// What caused the job to be offered -- see <see cref="PamRotationSource"/>.
    /// </summary>
    public PamRotationSource Source { get; set; }

    /// <summary>
    /// The earliest time the job can be claimed (UTC). Pushed out on retry (backoff) or release.
    /// </summary>
    public DateTime NextClaimableAt { get; set; }

    /// <summary>
    /// When the job times out if no attempt has succeeded by then (UTC).
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// The target system the job's rotation runs against.
    /// </summary>
    public Guid TargetSystemId { get; set; }
}
