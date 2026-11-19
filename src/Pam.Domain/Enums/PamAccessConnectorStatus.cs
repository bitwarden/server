namespace Bit.Pam.Enums;

/// <summary>
/// Lifecycle of a <see cref="Entities.PamAccessConnector"/>. Only an <see cref="Enabled"/> access connector may
/// authenticate, poll, or claim jobs; a <see cref="Disabled"/> one keeps its credential and can be re-enabled.
/// </summary>
public enum PamAccessConnectorStatus : byte
{
    Enabled = 0,
    Disabled = 1,
}
