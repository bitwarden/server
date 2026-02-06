using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Models.Data;

namespace Bit.Core.Tools.Services;

/// <summary>
/// Send Authorization service is responsible for checking if a Send can be accessed.
/// </summary>
public interface ISendAuthorizationService
{
    /// <summary>
    /// Checks if a <see cref="Send" /> can be accessed while updating the <see cref="Send" />, pushing a notification, and sending a reference event.
    /// </summary>
    /// <param name="send"><see cref="Send" /> used to determine access</param>
    /// <param name="password">A hashed and base64-encoded password. This is compared with the send's password to authorize access.</param>
    /// <returns><see cref="SendAccessResult" /> will be returned to determine if the user can access send.
    /// </returns>
    Task<SendAccessResult> AccessAsync(Send send, string password);
    SendAccessResult SendCanBeAccessed(Send send,
        string password);

    /// <summary>
    /// Hashes the password using the password hasher.
    /// </summary>
    /// <param name="password">Password to be hashed</param>
    /// <returns>Hashed password of the password given</returns>
    string HashPassword(string password);
}
