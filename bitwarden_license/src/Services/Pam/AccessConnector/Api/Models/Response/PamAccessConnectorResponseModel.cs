using  Bit.HttpExtensions;
using Bit.Pam.Enums;
using Bit.Services.Pam.AccessConnector.Models;
using Bit.Services.Pam.Api.Models.Response;

namespace Bit.Services.Pam.AccessConnector.Api.Models.Response;

/// <summary>
/// An access connector as the fleet-admin surface renders it: its derived liveness (spec <c>ConnectorConnection</c>)
/// and the target systems it is assigned to. The list view model for <c>GET access-connectors</c>.
/// </summary>
public class PamAccessConnectorResponseModel : ResponseModel
{
    public PamAccessConnectorResponseModel(PamAccessConnectorListItem item, string obj = "pamAccessConnector")
        : base(obj)
    {
        ArgumentNullException.ThrowIfNull(item);

        Id = item.Daemon.Id;
        OrganizationId = item.Daemon.OrganizationId;
        Name = item.Daemon.Name;
        Status = item.Daemon.Status;
        IsConnected = item.IsConnected;
        LastHeartbeatAt = item.Daemon.LastHeartbeatAt.AsUtc();
        AssignedTargetSystemIds = item.AssignedTargetSystemIds;
        CreationDate = item.Daemon.CreationDate.AsUtc();
        RevisionDate = item.Daemon.RevisionDate.AsUtc();
    }

    /// <summary>
    /// The access connector's unique identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The organization this access connector belongs to.
    /// </summary>
    public Guid OrganizationId { get; set; }

    /// <summary>
    /// The access connector's display label, shown wherever access connectors are listed and managed.
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// Whether the access connector may authenticate, poll, and claim jobs -- see
    /// <see cref="PamAccessConnectorStatus"/>. A disabled access connector keeps its credential and can be re-enabled.
    /// </summary>
    public PamAccessConnectorStatus Status { get; set; }

    /// <summary>
    /// Derived from <see cref="LastHeartbeatAt"/> against <c>PamRotationOptions.ConnectorOfflineAfter</c> -- spec
    /// <c>ConnectorConnection</c>.
    /// </summary>
    public bool IsConnected { get; set; }

    /// <summary>
    /// The last time the access connector polled or reported (UTC). Null until its first request; bumped only by the
    /// access connector's own requests, never by a sweep.
    /// </summary>
    public DateTime? LastHeartbeatAt { get; set; }

    /// <summary>
    /// The target systems this access connector is assigned to work. An access connector is offered rotation jobs only
    /// for the targets it is assigned.
    /// </summary>
    public IReadOnlyList<Guid> AssignedTargetSystemIds { get; set; } = [];

    /// <summary>
    /// When the access connector was registered (UTC).
    /// </summary>
    public DateTime CreationDate { get; set; }

    /// <summary>
    /// When the access connector was last modified (UTC).
    /// </summary>
    public DateTime RevisionDate { get; set; }
}
