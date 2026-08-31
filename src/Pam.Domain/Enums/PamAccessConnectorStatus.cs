namespace Bit.Pam.Enums;

/// <summary>
/// Lifecycle of a <see cref="Entities.PamDaemon"/>. Only an <see cref="Enabled"/> access connector may
/// authenticate, poll, or claim jobs. <see cref="Disabled"/> is a reversible pause — the access connector keeps its
/// credential and can be re-enabled; permanently removing an access connector (and invalidating its credential) is a
/// separate delete, not a status.
/// </summary>
public enum PamAccessConnectorStatus : byte
{
    Enabled = 0,
    Disabled = 1,
}
