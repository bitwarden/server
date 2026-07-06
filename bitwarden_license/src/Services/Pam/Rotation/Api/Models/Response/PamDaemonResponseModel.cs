using Bit.HttpExtensions;
using Bit.Pam.Enums;
using Bit.Services.Pam.Api.Models.Response;
using Bit.Services.Pam.Rotation.Models;

namespace Bit.Services.Pam.Rotation.Api.Models.Response;

/// <summary>
/// A rotation daemon as the fleet-admin surface renders it: its derived liveness (spec <c>DaemonConnection</c>) and
/// the target systems it is assigned to. The list view model for <c>GET rotation/daemons</c>.
/// </summary>
public class PamDaemonResponseModel : ResponseModel
{
    public PamDaemonResponseModel(PamDaemonListItem item)
        : base("pamDaemon")
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

    public Guid Id { get; }
    public Guid OrganizationId { get; }
    public string Name { get; }
    public PamDaemonStatus Status { get; }

    /// <summary>Derived from <see cref="LastHeartbeatAt"/> against <c>PamRotationOptions.DaemonOfflineAfter</c> -- spec <c>DaemonConnection</c>.</summary>
    public bool IsConnected { get; }

    public DateTime? LastHeartbeatAt { get; }
    public IReadOnlyList<Guid> AssignedTargetSystemIds { get; }
    public DateTime CreationDate { get; }
    public DateTime RevisionDate { get; }
}
