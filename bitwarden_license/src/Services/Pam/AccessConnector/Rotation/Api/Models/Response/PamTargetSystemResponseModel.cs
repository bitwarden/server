using Bit.HttpExtensions;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Services.Pam.Api.Models.Response;

namespace Bit.Services.Pam.AccessConnector.Rotation.Api.Models.Response;

/// <summary>A registered target system.</summary>
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

    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public string Name { get; set; } = null!;

    /// <summary>Decides which of the fields below carry a value.</summary>
    public PamTargetSystemMethod Method { get; set; }

    /// <summary>The integration an automatic target is rotated through. Null on a manual target.</summary>
    public PamTargetSystemKind? Kind { get; set; }

    /// <summary>
    /// The constraints the access connector must satisfy. On a manual target it is advisory, and null when none was
    /// set.
    /// </summary>
    public PamPasswordPolicyResponseModel? PasswordPolicy { get; set; }

    /// <summary>
    /// Whether the integration can terminate live sessions after a rotation; configs on this target can request
    /// termination only when true. Null on a manual target.
    /// </summary>
    public bool? SupportsSessionTermination { get; set; }

    /// <summary>Whether the target is offerable for rotation and accepts new rotation configs.</summary>
    public PamTargetSystemStatus Status { get; set; }

    public DateTime CreationDate { get; set; }

    public DateTime RevisionDate { get; set; }
}
