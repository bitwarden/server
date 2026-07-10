namespace Bit.Pam.Enums;

/// <summary>
/// Lifecycle of a <see cref="Entities.PamDaemon"/>. Only an <see cref="Enabled"/> daemon may authenticate, poll, or
/// claim jobs. <see cref="Disabled"/> is a reversible pause — the daemon keeps its credential and can be re-enabled;
/// permanently removing a daemon (and invalidating its credential) is a separate delete, not a status.
/// </summary>
public enum PamDaemonStatus : byte
{
    Enabled = 0,
    Disabled = 1,
}
