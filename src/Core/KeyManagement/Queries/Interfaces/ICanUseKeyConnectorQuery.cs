using Bit.Core.Entities;

namespace Bit.Core.KeyManagement.Queries.Interfaces;

/// <summary>
/// Query to verify if the user can use the key connector
/// </summary>
public interface ICanUseKeyConnectorQuery
{
    /// <summary>
    /// Throws an exception if the user cannot use the key connector
    /// </summary>
    /// <param name="user">User to validate</param>
    void VerifyCanUseKeyConnector(User user);
}
