using Bit.HttpExtensions;
using Bit.Pam.Enums;
using Bit.Services.Pam.AccessConnector.Models;
using Bit.Services.Pam.Api.Models.Response;

namespace Bit.Services.Pam.AccessConnector.Api.Models.Response;

/// <summary>An access connector with its derived liveness and target assignments.</summary>
public class PamAccessConnectorResponseModel : ResponseModel
{
    public PamAccessConnectorResponseModel(PamAccessConnectorListItem item, string obj = "pamAccessConnector")
        : base(obj)
    {
        ArgumentNullException.ThrowIfNull(item);

        Id = item.AccessConnector.Id;
        OrganizationId = item.AccessConnector.OrganizationId;
        Name = item.AccessConnector.Name;
        Status = item.AccessConnector.Status;
        IsConnected = item.IsConnected;
        LastHeartbeatAt = item.AccessConnector.LastHeartbeatAt.AsUtc();
        AssignedTargetSystemIds = item.AssignedTargetSystemIds;
        CreationDate = item.AccessConnector.CreationDate.AsUtc();
        RevisionDate = item.AccessConnector.RevisionDate.AsUtc();
    }

    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public string Name { get; set; } = null!;

    /// <summary>
    /// Whether the access connector may authenticate and claim jobs. A disabled one keeps its credential and can be
    /// re-enabled.
    /// </summary>
    public PamAccessConnectorStatus Status { get; set; }

    /// <summary>
    /// Whether <see cref="LastHeartbeatAt"/> is within <c>AccessConnectorOfflineAfter</c> of now (spec
    /// <c>ConnectorConnection</c>).
    /// </summary>
    public bool IsConnected { get; set; }

    /// <summary>The last time the access connector polled or reported. Null until its first request.</summary>
    public DateTime? LastHeartbeatAt { get; set; }

    /// <summary>The only target systems the access connector is offered rotation jobs for.</summary>
    public IReadOnlyList<Guid> AssignedTargetSystemIds { get; set; } = [];

    public DateTime CreationDate { get; set; }

    public DateTime RevisionDate { get; set; }
}
