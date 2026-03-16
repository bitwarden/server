using Bit.Core.Entities;
using Bit.Core.KeyManagement.Models.Data;

namespace Bit.Core.KeyManagement.Commands.Interfaces;

/// <summary>
/// Creates the user key and account cryptographic state for a new user registering
/// with Key Connector SSO configuration.
/// </summary>
public interface ISetKeyConnectorKeyCommand
{
    Task SetKeyConnectorKeyForUserAsync(User user, KeyConnectorKeysData keyConnectorKeysData);
}
