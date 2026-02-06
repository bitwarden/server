using Bit.Core.Tools.Models.Data;

#nullable enable

namespace Bit.Core.Tools.SendFeatures.Queries.Interfaces;

/// <summary>
/// Integration with authentication layer for generating send access claims.
/// </summary>
public interface ISendAuthenticationQuery
{
    /// <summary>
    /// Retrieves the authentication method of a Send.
    /// </summary>
    /// <param name="sendId">Identifies the send to inspect.</param>
    /// <returns>
    /// The authentication method that should be performed for the send.
    /// </returns>
    Task<SendAuthenticationMethod> GetAuthenticationMethod(Guid sendId);
}
