using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Services.Pam.Api.Models.Response;

namespace Bit.Services.Pam.Rotation.Api.Models.Response;

/// <summary>
/// The work snapshot handed back on a successful claim (spec <c>ClaimRotation</c>) -- everything the daemon needs
/// to execute the rotation without another round trip. Only meaningful when the claim command returns a
/// <see cref="Bit.Pam.Enums.PamRotationClaimOutcome.Claimed"/> result; the command throws otherwise, so every
/// field here is populated.
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

    /// <summary>
    /// The attempt opened by this claim -- the id the daemon reports its outcome against.
    /// </summary>
    public Guid AttemptId { get; }

    /// <summary>
    /// The rotation job the claim was taken on.
    /// </summary>
    public Guid JobId { get; }

    /// <summary>
    /// What caused the job to be offered -- see <see cref="PamRotationSource"/>.
    /// </summary>
    public PamRotationSource Source { get; }

    /// <summary>
    /// The target system the rotation runs against.
    /// </summary>
    public Guid TargetSystemId { get; }

    /// <summary>
    /// The target system's display name.
    /// </summary>
    public string TargetSystemName { get; }

    /// <summary>
    /// The connector the target is rotated through -- see <see cref="PamTargetSystemKind"/>.
    /// </summary>
    public PamTargetSystemKind? Kind { get; }

    /// <summary>
    /// The password-generation constraints the daemon must satisfy for this rotation.
    /// </summary>
    public PamPasswordPolicyResponseModel? PasswordPolicy { get; }

    /// <summary>
    /// The organization cipher holding the credential to rotate.
    /// </summary>
    public Guid CipherId { get; }

    /// <summary>
    /// The account to rotate on the target system. Opaque to the server -- never parsed; only the daemon
    /// interprets it.
    /// </summary>
    public string AccountIdentity { get; }

    /// <summary>
    /// When true, the daemon terminates the account's live sessions after rotating.
    /// </summary>
    public bool TerminateSessions { get; }

    /// <summary>
    /// The claim's lease deadline. The daemon must finish -- or at least keep heartbeating -- before this, or the
    /// release sweep may reclaim the job out from under it once its heartbeat also goes stale.
    /// </summary>
    public DateTime ExecuteBy { get; }
}
