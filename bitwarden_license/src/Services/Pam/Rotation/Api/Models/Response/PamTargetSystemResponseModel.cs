using Bit.HttpExtensions;
using Bit.Pam.Enums;

namespace Bit.Services.Pam.Rotation.Api.Models.Response;

/// <summary>A registered target system, as the fleet-admin surface renders it. The view model for <c>GET rotation/target-systems</c>.</summary>
public class PamTargetSystemResponseModel : ResponseModel
{
    public PamTargetSystemResponseModel()
        : base("pamTargetSystem")
    {
    }

    /// <summary>
    /// The target system's unique identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The organization this target system belongs to.
    /// </summary>
    public Guid OrganizationId { get; set; }

    /// <summary>
    /// The target system's display name, shown wherever targets are listed and managed.
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// How the target's credentials are rotated -- see <see cref="PamTargetSystemMethod"/>. Decides which of the
    /// fields below carry a value.
    /// </summary>
    public PamTargetSystemMethod Method { get; set; }

    /// <summary>
    /// The connector an automatic target is rotated through -- see <see cref="PamTargetSystemKind"/>. Null on a
    /// manual target, which has no connector.
    /// </summary>
    public PamTargetSystemKind? Kind { get; set; }

    /// <summary>
    /// The password-generation constraints the daemon must satisfy when rotating credentials on this target. Null
    /// on a manual target.
    /// </summary>
    public PamPasswordPolicyResponseModel? PasswordPolicy { get; set; }

    /// <summary>
    /// Whether the connector can terminate the account's live sessions after a rotation; gates whether rotation
    /// configs on this target may request session termination. Null on a manual target.
    /// </summary>
    public bool? SupportsSessionTermination { get; set; }

    /// <summary>
    /// Whether the target is offerable for rotation and assignable to a daemon -- see
    /// <see cref="PamTargetSystemStatus"/>.
    /// </summary>
    public PamTargetSystemStatus Status { get; set; }

    /// <summary>
    /// When the target system was registered (UTC).
    /// </summary>
    public DateTime CreationDate { get; set; }

    /// <summary>
    /// When the target system was last modified (UTC).
    /// </summary>
    public DateTime RevisionDate { get; set; }
}
