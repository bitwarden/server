using Bit.HttpExtensions;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
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
        Status = targetSystem.Status;
        SealedData = targetSystem.SealedData;
        WrappedKey = targetSystem.WrappedKey;
        CreationDate = targetSystem.CreationDate.AsUtc();
        RevisionDate = targetSystem.RevisionDate.AsUtc();
    }

    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public string Name { get; set; } = null!;

    public PamTargetSystemMethod Method { get; set; }

    /// <summary>Whether the target is offerable for rotation and accepts new rotation configs.</summary>
    public PamTargetSystemStatus Status { get; set; }

    /// <summary>The target system's sealed blob.</summary>
    public string? SealedData { get; set; }

    /// <summary>The content key of the sealed blob, wrapped by the organization key.</summary>
    public string? WrappedKey { get; set; }

    public DateTime CreationDate { get; set; }

    public DateTime RevisionDate { get; set; }
}
