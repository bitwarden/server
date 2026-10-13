using Bit.HttpExtensions;
using Bit.Pam.Enums;

namespace Bit.Services.Pam.AccessConnector.Rotation.Api.Models.Response;

/// <summary>
/// A claimable rotation job, as the access connector's poll returns it (the candidate set spec <c>ClaimRotation</c>
/// claims from).
/// </summary>
public class ClaimableRotationJobResponseModel : ResponseModel
{
    public ClaimableRotationJobResponseModel()
        : base("pamRotationJob")
    {
    }

    public Guid JobId { get; set; }

    public PamRotationSource Source { get; set; }

    /// <summary>The earliest time the job can be claimed; pushed out by retry backoff or a release.</summary>
    public DateTime NextClaimableAt { get; set; }

    /// <summary>When the job times out unless an attempt has succeeded.</summary>
    public DateTime ExpiresAt { get; set; }

    public Guid TargetSystemId { get; set; }
}
