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
    public PamDaemonResponseModel(PamDaemonListItem item, string obj = "pamDaemon")
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
    /// The daemon's unique identifier.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// The organization this daemon belongs to.
    /// </summary>
    public Guid OrganizationId { get; }

    /// <summary>
    /// The daemon's display label, shown wherever daemons are listed and managed.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Whether the daemon may authenticate, poll, and claim jobs -- see <see cref="PamDaemonStatus"/>. A disabled
    /// daemon keeps its credential and can be re-enabled.
    /// </summary>
    public PamDaemonStatus Status { get; }

    /// <summary>
    /// Derived from <see cref="LastHeartbeatAt"/> against <c>PamRotationOptions.DaemonOfflineAfter</c> -- spec
    /// <c>DaemonConnection</c>.
    /// </summary>
    public bool IsConnected { get; }

    /// <summary>
    /// The last time the daemon polled or reported (UTC). Null until its first request; bumped only by the
    /// daemon's own requests, never by a sweep.
    /// </summary>
    public DateTime? LastHeartbeatAt { get; }

    /// <summary>
    /// The target systems this daemon is assigned to work. A daemon is offered rotation jobs only for the targets
    /// it is assigned.
    /// </summary>
    public IReadOnlyList<Guid> AssignedTargetSystemIds { get; }

    /// <summary>
    /// When the daemon was registered (UTC).
    /// </summary>
    public DateTime CreationDate { get; }

    /// <summary>
    /// When the daemon was last modified (UTC).
    /// </summary>
    public DateTime RevisionDate { get; }
}
