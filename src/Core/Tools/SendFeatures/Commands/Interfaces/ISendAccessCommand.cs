using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Models.Data;

namespace Bit.Core.Tools.SendFeatures.Commands.Interfaces;

/// <summary>
/// SendAccessCommand interface provides methods for accessing a <see cref="Send" /> as a recipient,
/// rather than as its owner.
/// </summary>
public interface ISendAccessCommand
{
    /// <summary>
    /// If a File type Send can be downloaded, retrieves the download URL.
    /// </summary>
    /// <param name="send">The <see cref="Send" /> this command acts upon</param>
    /// <param name="fileId">The fileId to be downloaded</param>
    /// <returns>
    /// A tuple wrapping the download URL string and <see cref="SendAccessResult" /> indicating whether access was granted
    /// </returns>
    /// <remarks>
    /// This method is intended for authenticated endpoints where authentication has already been validated.
    /// Returns <see cref="SendAccessResult.Denied" /> when the Send is disabled, MaxAccessCount has been reached,
    /// expiration date has passed, or deletion date has been reached.
    /// </remarks>
    Task<(string, SendAccessResult)> GetSendFileDownloadUrlAsync(Send send, string fileId);

    /// <summary>
    /// Records an access against the <see cref="Send" /> by incrementing its access count, persisting
    /// the change, and pushing a sync notification to the owner's clients.
    /// </summary>
    /// <param name="send">The <see cref="Send" /> that was accessed</param>
    /// <returns>Task completes once the new access count has been persisted and the sync pushed</returns>
    /// <remarks>
    /// Callers are responsible for deciding when an access is countable. Text and Item Sends count every
    /// access; File Sends only count a completed download.
    /// </remarks>
    Task IncrementAccessCountAsync(Send send);

    /// <summary>
    /// Determines whether a <see cref="Send" /> can be accessed based on its current state.
    /// </summary>
    /// <param name="send">The <see cref="Send" /> to evaluate for access</param>
    /// <returns><see langword="true" /> if the Send can be accessed, otherwise <see langword="false" /></returns>
    /// <remarks>
    /// This method checks if the Send is disabled, if MaxAccessCount has been reached,
    /// if the expiration date has passed, or if the deletion date has been reached.
    /// </remarks>
    static bool SendCanBeAccessed(Send send)
    {
        var now = DateTime.UtcNow;
        if (send.MaxAccessCount.GetValueOrDefault(int.MaxValue) <= send.AccessCount ||
            send.ExpirationDate.GetValueOrDefault(DateTime.MaxValue) < now ||
            send.Disabled ||
            send.DeletionDate <= now)
        {
            return false;
        }

        return true;
    }
}
