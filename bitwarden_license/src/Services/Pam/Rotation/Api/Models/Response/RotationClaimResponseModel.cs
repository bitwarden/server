using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Services.Pam.Api.Models.Response;

namespace Bit.Services.Pam.Rotation.Api.Models.Response;

/// <summary>
/// The work snapshot handed back on a successful claim (spec <c>ClaimRotation</c>) -- everything the daemon needs
/// to execute the rotation without another round trip. Only meaningful when the claim command returns a
/// <see cref="Bit.Pam.Enums.PamRotationClaimOutcome.Claimed"/> result; the command throws otherwise (409 lost race,
/// 404 never eligible), so every field here is populated.
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

    public Guid AttemptId { get; }
    public Guid JobId { get; }
    public PamRotationSource Source { get; }
    public Guid TargetSystemId { get; }
    public string TargetSystemName { get; }
    public PamTargetSystemKind? Kind { get; }
    public PamPasswordPolicyResponseModel? PasswordPolicy { get; }
    public Guid CipherId { get; }

    /// <summary>Opaque to the server -- never parsed; only the daemon interprets it.</summary>
    public string AccountIdentity { get; }

    public bool TerminateSessions { get; }

    /// <summary>
    /// The claim's lease deadline. The daemon must finish -- or at least keep heartbeating -- before this, or the
    /// release sweep may reclaim the job out from under it once its heartbeat also goes stale.
    /// </summary>
    public DateTime ExecuteBy { get; }
}
