namespace Bit.Core.Auth.Enums;

/// <summary>
/// The kind of connected app a <see cref="Entities.ConnectedApp"/> record represents. Agent access is the
/// only type today; the discriminator lets other "connected app" integrations reuse the same storage later.
/// </summary>
public enum ConnectedAppType : byte
{
    AgentAccess = 1,
}
