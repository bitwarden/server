using Bit.Core.Tools.Entities;

namespace Bit.Core.Tools.Services;

public interface ISendValidationService
{
    /// <summary>
    /// Gets the maximum file size for a Send.
    /// </summary>
    /// <returns>Max file size for a Send</returns>
    long GetMaxFileSize();

    /// <summary>
    /// Gets the maximum file size for a Send in a human-readable format.
    /// </summary>
    /// <returns>Max file size for a Send in human-readable format</returns>
    string GetMaxFileSizeReadable();

    /// <summary>
    /// Validates the Send file.
    /// </summary>
    /// <param name="send">Send object needed to validate file</param>
    /// <returns>Boolean whether the file was valid or not</returns>
    Task<bool> ValidateSendFile(Send send);

    /// <summary>
    /// Validates a file can be saved by specified user.
    /// </summary>
    /// <param name="userId">UserId needed to validate file for specific user</param>
    /// <param name="send">Send object needed to help validate file</param>
    /// <returns>Task object for async operations</returns>
    Task ValidateUserCanSaveAsync(Guid? userId, Send send);

    /// <summary>
    /// Validates a file can be saved by specified user with different policy based on feature flag
    /// </summary>
    /// <param name="userId">UserId needed to validate file for specific user</param>
    /// <param name="send">Send object needed to help validate file</param>
    /// <returns>Task object for async operations</returns>
    Task ValidateUserCanSaveAsync_vNext(Guid? userId, Send send);

    /// <summary>
    /// Calculates the remaining storage for a Send.
    /// </summary>
    /// <param name="send"></param>
    /// <returns>Task object for async operations</returns>
    Task<long> StorageRemainingForSendAsync(Send send);
}
