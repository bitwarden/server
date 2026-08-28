using Bit.HttpExtensions;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Services.Pam.Api.Models.Response;

namespace Bit.Services.Pam.Rotation.Api.Models.Response;

/// <summary>A registered target system, as the fleet-admin surface renders it. The view model for <c>GET rotation/target-systems</c>.</summary>
public class PamTargetSystemResponseModel : ResponseModel
{
    public PamTargetSystemResponseModel(PamTargetSystem targetSystem)
        : base("pamTargetSystem")
    {
        ArgumentNullException.ThrowIfNull(targetSystem);

        Id = targetSystem.Id;
        OrganizationId = targetSystem.OrganizationId;
        Name = targetSystem.Name;
        Method = targetSystem.Method;
        Kind = targetSystem.Kind;
        var policy = PamPasswordPolicy.Parse(targetSystem.PasswordPolicy);
        PasswordPolicy = policy is null ? null : new PamPasswordPolicyResponseModel(policy);
        SupportsSessionTermination = targetSystem.SupportsSessionTermination;
        Status = targetSystem.Status;
        CreationDate = targetSystem.CreationDate.AsUtc();
        RevisionDate = targetSystem.RevisionDate.AsUtc();
    }

    /// <summary>
    /// The target system's unique identifier.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// The organization this target system belongs to.
    /// </summary>
    public Guid OrganizationId { get; }

    /// <summary>
    /// The target system's display name, shown wherever targets are listed and managed.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// How the target's credentials are rotated -- see <see cref="PamTargetSystemMethod"/>. Decides which of the
    /// fields below carry a value.
    /// </summary>
    public PamTargetSystemMethod Method { get; }

    /// <summary>
    /// The connector an automatic target is rotated through -- see <see cref="PamTargetSystemKind"/>. Null on a
    /// manual target, which has no connector.
    /// </summary>
    public PamTargetSystemKind? Kind { get; }

    /// <summary>
    /// The password-generation constraints the daemon must satisfy when rotating credentials on this target. Null
    /// on a manual target.
    /// </summary>
    public PamPasswordPolicyResponseModel? PasswordPolicy { get; }

    /// <summary>
    /// Whether the connector can terminate the account's live sessions after a rotation; gates whether rotation
    /// configs on this target may request session termination. Null on a manual target.
    /// </summary>
    public bool? SupportsSessionTermination { get; }

    /// <summary>
    /// Whether the target is offerable for rotation and assignable to a daemon -- see
    /// <see cref="PamTargetSystemStatus"/>.
    /// </summary>
    public PamTargetSystemStatus Status { get; }

    /// <summary>
    /// When the target system was registered (UTC).
    /// </summary>
    public DateTime CreationDate { get; }

    /// <summary>
    /// When the target system was last modified (UTC).
    /// </summary>
    public DateTime RevisionDate { get; }
}
