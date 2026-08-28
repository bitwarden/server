using Bit.Pam.Enums;

namespace Bit.Services.Pam.Rotation.Api.Models.Response;

/// <summary>
/// The work snapshot handed back on a successful claim (spec <c>ClaimRotation</c>) -- everything the daemon needs
/// to execute the rotation without another round trip. Returned only when the claim was actually won; a lost or
/// ineligible claim is an error response instead, so every field here is populated.
/// </summary>
public class RotationClaimResponseModel
{
    /// <summary>
    /// The attempt opened by this claim -- the id the daemon reports its outcome against.
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
    /// The connector the target is rotated through -- see <see cref="PamTargetSystemKind"/>.
    /// </summary>
    public PamTargetSystemKind? Kind { get; set; }

    /// <summary>
    /// The password-generation constraints the daemon must satisfy for this rotation.
    /// </summary>
    public PamPasswordPolicyResponseModel? PasswordPolicy { get; set; }

    /// <summary>
    /// The organization cipher holding the credential to rotate.
    /// </summary>
    public Guid CipherId { get; set; }

    /// <summary>
    /// The account to rotate on the target system. Opaque to the server -- never parsed; only the daemon
    /// interprets it.
    /// </summary>
    public string AccountIdentity { get; set; } = null!;

    /// <summary>
    /// When true, the daemon terminates the account's live sessions after rotating.
    /// </summary>
    public bool TerminateSessions { get; set; }

    /// <summary>
    /// The claim's lease deadline. The daemon must finish -- or at least keep heartbeating -- before this, or the
    /// release sweep may reclaim the job out from under it once its heartbeat also goes stale.
    /// </summary>
    public DateTime ExecuteBy { get; set; }
}
