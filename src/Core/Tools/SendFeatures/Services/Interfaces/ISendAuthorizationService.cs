using Bit.Core.Tools.Entities;

namespace Bit.Core.Tools.Services;

/// <summary>
/// Send Authorization service is responsible for checking if a Send can be accessed.
/// </summary>
public interface ISendAuthorizationService
{
    /// <summary>
    /// Checks if a Send can be accessed while updating the Send, pushing a notification, and sending a reference event.
    /// </summary>
    /// <param name="sendId"></param>
    /// <param name="password"></param>
    /// <returns></returns>
    Task<(Send, bool, bool)> AccessAsync(Guid sendId, string password);
    (bool grant, bool passwordRequiredError, bool passwordInvalidError) SendCanBeAccessed(Send send,
        string password);
    /// <summary>
    /// Hashes the password using the password hasher.
    /// </summary>
    /// <param name="password"></param>
    /// <returns></returns>
    string HashPassword(string password);
}
