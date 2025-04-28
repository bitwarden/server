using Bit.Core.Tools.Entities;

namespace Bit.Core.Tools.Services;

public interface ISendValidationService
{
    /// <summary>
    /// Gets the maximum file size for a Send.
    /// </summary>
    /// <returns>Max file size for a <see cref="Send" /> </returns>
    long GetMaxFileSize();

    /// <summary>
    /// Gets the maximum file size for a Send in a human-readable format.
    /// </summary>
    /// <returns>Max file size for a <see cref="Send" />  in human-readable format</returns>
    string GetMaxFileSizeReadable();

    /// <summary>
    /// Validates the Send file.
    /// </summary>
    /// <param name="send"><see cref="Send" /> needed to validate file</param>
    /// <returns>Boolean whether the file was valid or not</returns>
    Task<bool> ValidateSendFile(Send send);

    /// <summary>
    /// Validates a file can be saved by specified user.
    /// </summary>
    /// <param name="userId"><see cref="Guid" /> needed to validate file for specific user</param>
    /// <param name="send"><see cref="Send" /> needed to help validate file</param>
    /// <returns>Task completes when a conditional statement has been met it will return out of the method or
    /// throw a BadRequestException.
    /// </returns>
    Task ValidateUserCanSaveAsync(Guid? userId, Send send);

    /// <summary>
    /// Validates a file can be saved by specified user with different policy based on feature flag
    /// </summary>
    /// <param name="userId"><see cref="Guid" /> needed to validate file for specific user</param>
    /// <param name="send"><see cref="Send" /> needed to help validate file</param>
    /// <returns>Task completes when a conditional statement has been met it will return out of the method or
    /// throw a BadRequestException.
    /// </returns>
    Task ValidateUserCanSaveAsync_vNext(Guid? userId, Send send);

    /// <summary>
    /// Calculates the remaining storage for a Send.
    /// </summary>
    /// <param name="send"><see cref="Send" /> needed to help calculate remaining storage</param>
    /// <returns>Long with the remaining bytes for storage or will throw a BadRequestException if user cannot access
    /// file or email is not verified.
    /// </returns>
    Task<long> StorageRemainingForSendAsync(Send send);
}
