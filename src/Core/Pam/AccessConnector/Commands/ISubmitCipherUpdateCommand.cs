namespace Bit.Core.Pam.AccessConnector.Commands;

public interface ISubmitCipherUpdateCommand
{
    /// <summary>
    /// Writes the rotated secret to the cipher under an atomic write-capability check. Throws
    /// <see cref="Bit.Core.Exceptions.NotFoundException"/> for an unknown attempt and
    /// <see cref="Bit.Core.Exceptions.ConflictException"/> for a lost capability or a stale revision.
    /// </summary>
    /// <param name="cipherKey">
    /// The cipher's wrapped key, stored in the same atomic write as <paramref name="cipherDataJson"/> because the data
    /// is encrypted under it. Null stores no item key.
    /// </param>
    Task SubmitAsync(
        Guid accessConnectorId,
        Guid attemptId,
        string cipherDataJson,
        string? cipherKey,
        DateTime lastKnownRevisionDate);
}
