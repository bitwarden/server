namespace Bit.Services.Pam.AccessConnector.Commands.Interfaces;

public interface ISubmitCipherUpdateCommand
{
    /// <summary>
    /// Writes the rotated secret to the cipher under an atomic write-capability check. Throws
    /// <see cref="Bit.Core.Exceptions.NotFoundException"/> for an unknown attempt and
    /// <see cref="Bit.Core.Exceptions.ConflictException"/> for a lost capability or a stale revision.
    /// </summary>
    Task SubmitAsync(Guid accessConnectorId, Guid attemptId, string cipherDataJson, DateTime lastKnownRevisionDate);
}
