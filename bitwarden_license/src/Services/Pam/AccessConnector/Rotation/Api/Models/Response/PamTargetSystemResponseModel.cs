using Bit.HttpExtensions;
using Bit.Pam.Enums;

namespace Bit.Services.Pam.AccessConnector.Rotation.Api.Models.Response;

/// <summary>A registered target system.</summary>
public class PamTargetSystemResponseModel : ResponseModel
{
    public PamTargetSystemResponseModel()
        : base("pamTargetSystem")
    {
    }

    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public string Name { get; set; } = null!;

    public PamTargetSystemMethod Method { get; set; }

    /// <summary>Whether the target is offerable for rotation and accepts new rotation configs.</summary>
    public PamTargetSystemStatus Status { get; set; }

    /// <summary>The target system's sealed blob.</summary>
    public string? SealedData { get; set; }

    public DateTime CreationDate { get; set; }

    public DateTime RevisionDate { get; set; }
}
