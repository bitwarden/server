using Bit.Pam.Entities;

namespace Bit.Services.Pam.Rotation.Models;

/// <summary>
/// A rotation daemon together with its derived liveness (spec <c>DaemonConnection</c>, see
/// <c>PamRotationRules.IsConnected</c>) and the target systems it is assigned to — the list view model for the
/// daemons admin surface.
/// </summary>
public sealed record PamDaemonListItem(
    PamDaemon Daemon,
    bool IsConnected,
    IReadOnlyList<Guid> AssignedTargetSystemIds);
