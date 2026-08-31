using  Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Services.Pam.Api.Models.Response;

namespace Bit.Services.Pam.AccessConnector.Rotation.Api.Models.Response;

/// <summary>
/// The work snapshot handed back on a successful claim (spec <c>ClaimRotation</c>) -- everything the access connector
/// needs to execute the rotation without another round trip. Returned only when the claim was actually won; a lost or
/// ineligible claim is an error response instead, so every field here is populated.
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
    /// The attempt opened by this claim -- the id the access connector reports its outcome against.
    /// </summary>
    public Guid AttemptId { get; set; }

    /// <summary>
    /// The rotation job the claim was taken on.
    /// </summary>
    public Guid JobId { get; set; }

    /// <summary>
    /// What caused the job to be offered -- see <see cref="PamRotationSource"/>.
    /// </summary>
    public PamRotationSource Source { get; set; }

    /// <summary>
    /// The target system the rotation runs against.
    /// </summary>
    public Guid TargetSystemId { get; set; }

    /// <summary>
    /// The target system's display name.
    /// </summary>
    public string TargetSystemName { get; set; } = null!;

    /// <summary>
    /// The integration the target is rotated through -- see <see cref="PamTargetSystemKind"/>.
    /// </summary>
    public PamTargetSystemKind? Kind { get; set; }

    /// <summary>
    /// The password-generation constraints the access connector must satisfy for this rotation.
    /// </summary>
    public PamPasswordPolicyResponseModel? PasswordPolicy { get; set; }

    /// <summary>
    /// The organization cipher holding the credential to rotate.
    /// </summary>
    public Guid CipherId { get; set; }

    /// <summary>
    /// The account to rotate on the target system. Opaque to the server -- never parsed; only the access connector
    /// interprets it.
    /// </summary>
    public string AccountIdentity { get; set; } = null!;

    /// <summary>
    /// When true, the access connector terminates the account's live sessions after rotating.
    /// </summary>
    public bool TerminateSessions { get; set; }

    /// <summary>
    /// The claim's lease deadline. The access connector must finish -- or at least keep heartbeating -- before this, or
    /// the release sweep may reclaim the job out from under it once its heartbeat also goes stale.
    /// </summary>
    public DateTime ExecuteBy { get; set; }
}
