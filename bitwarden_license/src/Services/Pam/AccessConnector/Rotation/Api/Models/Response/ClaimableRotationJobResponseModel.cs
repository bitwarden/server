using Bit.HttpExtensions;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Services.Pam.Api.Models.Response;

namespace Bit.Services.Pam.AccessConnector.Rotation.Api.Models.Response;

/// <summary>
/// A claimable rotation job, as the access connector's poll returns it (the candidate set spec <c>ClaimRotation</c>
/// claims from).
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

    public Guid JobId { get; set; }

    public PamRotationSource Source { get; set; }

    /// <summary>The earliest time the job can be claimed; pushed out by retry backoff or a release.</summary>
    public DateTime NextClaimableAt { get; set; }

    /// <summary>When the job times out unless an attempt has succeeded.</summary>
    public DateTime ExpiresAt { get; set; }

    public Guid TargetSystemId { get; set; }
}
