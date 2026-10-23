using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Models.Data;

namespace Bit.Core.KeyManagement.Commands.Interfaces;

/// <summary>
/// Creates the user key and account cryptographic state for a new user registering
/// with Key Connector SSO configuration.
/// </summary>
public interface ISetKeyConnectorKeyCommand
{
    /// <summary>
    /// Sets the Key Connector wrapped user key and V2 account cryptographic state for the user.
    /// </summary>
    /// <param name="user">User to set the keys for. Must be allowed to use Key Connector.</param>
    /// <param name="keyConnectorKeysData">Key Connector wrapped user key, account keys, and org SSO identifier.</param>
    /// <param name="acceptOrganizationInvite">
    /// True to accept the user's pending organization membership. False leaves the membership in its current state.
    /// </param>
    /// <exception cref="BadRequestException">
    /// Thrown if the user cannot use Key Connector, the organization SSO identifier is invalid, or the user
    /// is not a member of the organization.
    /// </exception>
    Task SetKeyConnectorKeyForUserAsync(User user, KeyConnectorKeysData keyConnectorKeysData,
        bool acceptOrganizationInvite = true);
}
