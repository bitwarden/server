using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Services.Pam.Api.Models.Response;

namespace Bit.Services.Pam.AccessConnector.Rotation.Api.Models.Response;

/// <summary>
/// The work snapshot a won claim returns (spec <c>ClaimRotation</c>), with everything needed to execute the rotation.
/// A lost or ineligible claim gets an error response instead, so every field is populated.
/// </summary>
public class RotationClaimResponseModel
{
    public RotationClaimResponseModel(PamRotationClaimResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        AttemptId = result.AttemptId!.Value;
        JobId = result.JobId!.Value;
        Source = result.Source!.Value;
        TargetSystemId = result.TargetSystemId!.Value;
        TargetSystemName = result.TargetSystemName!;
        TargetSystemSealedData = result.TargetSystemSealedData!;
        TargetSystemWrappedKey = result.TargetSystemWrappedKey!;
        CipherId = result.CipherId!.Value;
        RotationConfigSealedData = result.RotationConfigSealedData!;
        RotationConfigWrappedKey = result.RotationConfigWrappedKey!;
        ExecuteBy = result.ExecuteBy!.Value.AsUtc();
    }

    /// <summary>The attempt this claim opened, which the outcome is reported against.</summary>
    public Guid AttemptId { get; set; }

    public Guid JobId { get; set; }

    public PamRotationSource Source { get; set; }

    public Guid TargetSystemId { get; set; }

    public string TargetSystemName { get; set; } = null!;

    /// <summary>The target system's sealed blob.</summary>
    public string TargetSystemSealedData { get; set; } = null!;

    /// <summary>The content key of the target system's sealed blob, wrapped by the organization key.</summary>
    public string TargetSystemWrappedKey { get; set; } = null!;

    public Guid CipherId { get; set; }

    /// <summary>The rotation config's sealed blob.</summary>
    public string RotationConfigSealedData { get; set; } = null!;

    /// <summary>The content key of the rotation config's sealed blob, wrapped by the organization key.</summary>
    public string RotationConfigWrappedKey { get; set; } = null!;

    /// <summary>
    /// The claim's lease deadline. Past it, the release sweep may reclaim the job if the access connector's heartbeat
    /// has also gone stale.
    /// </summary>
    public DateTime ExecuteBy { get; set; }
}
