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
        Kind = result.Kind;
        var policy = PamPasswordPolicy.Parse(result.PasswordPolicy);
        PasswordPolicy = policy is null ? null : new PamPasswordPolicyResponseModel(policy);
        CipherId = result.CipherId!.Value;
        AccountIdentity = result.AccountIdentity!;
        TerminateSessions = result.TerminateSessions!.Value;
        ExecuteBy = result.ExecuteBy!.Value.AsUtc();
    }

    /// <summary>The attempt this claim opened, which the outcome is reported against.</summary>
    public Guid AttemptId { get; set; }

    public Guid JobId { get; set; }

    public PamRotationSource Source { get; set; }

    public Guid TargetSystemId { get; set; }

    public string TargetSystemName { get; set; } = null!;

    /// <summary>The integration the target is rotated through.</summary>
    public PamTargetSystemKind? Kind { get; set; }

    public PamPasswordPolicyResponseModel? PasswordPolicy { get; set; }

    public Guid CipherId { get; set; }

    /// <summary>The account to rotate on the target system, opaque to the server.</summary>
    public string AccountIdentity { get; set; } = null!;

    /// <summary>When true, the access connector terminates the account's live sessions after rotating.</summary>
    public bool TerminateSessions { get; set; }

    /// <summary>
    /// The claim's lease deadline. Past it, the release sweep may reclaim the job if the access connector's heartbeat
    /// has also gone stale.
    /// </summary>
    public DateTime ExecuteBy { get; set; }
}
