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
    /// <param name="sendId"><see cref="Guid" /> of the <see cref="Send" /> needing to be accessed</param>
    /// <param name="password">A hashed and base64-encoded password. This is compared with the send's password to authorize access.</param>
    /// <returns>Async Task object with Tuple containing the Send object, boolean that identifies if
    /// passwordRequiredError occurred, and another boolean that identifies if passwordInvalidError occurred.
    /// </returns>
    Task<(Send, bool, bool)> AccessAsync(Guid sendId, string password);
    (bool grant, bool passwordRequiredError, bool passwordInvalidError) SendCanBeAccessed(Send send,
        string password);
    /// <summary>
    /// Hashes the password using the password hasher.
    /// </summary>
    /// <param name="password">Password to be hashed</param>
    /// <returns>Hashed password of the password given</returns>
    string HashPassword(string password);
}
