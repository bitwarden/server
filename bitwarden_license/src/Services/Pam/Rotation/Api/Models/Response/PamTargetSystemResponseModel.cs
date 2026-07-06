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

    public Guid Id { get; }
    public Guid OrganizationId { get; }
    public string Name { get; }
    public PamTargetSystemMethod Method { get; }
    public PamTargetSystemKind? Kind { get; }
    public PamPasswordPolicyResponseModel? PasswordPolicy { get; }
    public bool? SupportsSessionTermination { get; }
    public PamTargetSystemStatus Status { get; }
    public DateTime CreationDate { get; }
    public DateTime RevisionDate { get; }
}
