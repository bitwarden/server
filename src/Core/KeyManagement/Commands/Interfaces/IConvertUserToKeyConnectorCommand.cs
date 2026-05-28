using Bit.Core.Entities;

namespace Bit.Core.KeyManagement.Commands.Interfaces;

/// <summary>
/// Converts an existing master-password user into a Key Connector user — clears the master
/// password credential, marks the account as using Key Connector, optionally rotates the
/// user key wrap to a Key-Connector-wrapped form, persists the user, and emits an event.
/// </summary>
public interface IConvertUserToKeyConnectorCommand
{
    Task ConvertAsync(User user, string? keyConnectorKeyWrappedUserKey = null);
}
