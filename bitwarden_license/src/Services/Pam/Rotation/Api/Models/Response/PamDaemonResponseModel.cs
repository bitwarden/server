using Bit.HttpExtensions;
using Bit.Pam.Enums;

namespace Bit.Services.Pam.Rotation.Api.Models.Response;

/// <summary>
/// A rotation daemon as the fleet-admin surface renders it: its derived liveness (spec <c>DaemonConnection</c>) and
/// the target systems it is assigned to. The list view model for <c>GET rotation/daemons</c>.
/// </summary>
public class PamDaemonResponseModel : ResponseModel
{
    public PamDaemonResponseModel(string obj = "pamDaemon")
        : base(obj)
    {
    }

    /// <summary>
    /// The daemon's unique identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The organization this daemon belongs to.
    /// </summary>
    public Guid OrganizationId { get; set; }

    /// <summary>
    /// The daemon's display label, shown wherever daemons are listed and managed.
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// Whether the daemon may authenticate, poll, and claim jobs -- see <see cref="PamDaemonStatus"/>. A disabled
    /// daemon keeps its credential and can be re-enabled.
    /// </summary>
    public PamDaemonStatus Status { get; set; }

    /// <summary>
    /// Derived from <see cref="LastHeartbeatAt"/> against <c>PamRotationOptions.DaemonOfflineAfter</c> -- spec
    /// <c>DaemonConnection</c>.
    /// </summary>
    public bool IsConnected { get; set; }

    /// <summary>
    /// The last time the daemon polled or reported (UTC). Null until its first request; bumped only by the
    /// daemon's own requests, never by a sweep.
    /// </summary>
    public DateTime? LastHeartbeatAt { get; set; }

    /// <summary>
    /// The target systems this daemon is assigned to work. A daemon is offered rotation jobs only for the targets
    /// it is assigned.
    /// </summary>
    public IReadOnlyList<Guid> AssignedTargetSystemIds { get; set; } = [];

    /// <summary>
    /// When the daemon was registered (UTC).
    /// </summary>
    public DateTime CreationDate { get; set; }

    /// <summary>
    /// When the daemon was last modified (UTC).
    /// </summary>
    public DateTime RevisionDate { get; set; }
}
