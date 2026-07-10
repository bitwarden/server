namespace Bit.Services.Pam.Rotation.Commands.Interfaces;

public interface ISetDaemonStatusCommand
{
    /// <summary>
    /// Enables or disables a rotation daemon. Disabling is a reversible pause: the daemon keeps its credential but
    /// stops authenticating and claiming jobs (see <c>PamDaemonClientProvider</c> and <c>DaemonRequestEndpointFilter</c>,
    /// which admit only Enabled daemons); enabling reverses it. Guard: the daemon must not already be in the requested
    /// state. Permanent removal is a separate delete, not a status change.
    /// </summary>
    Task SetStatusAsync(Guid organizationId, Guid actingUserId, Guid daemonId, bool enable);
}
