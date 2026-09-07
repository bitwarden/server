namespace Bit.Services.Pam.AccessConnector.Commands.Interfaces;

public interface ISubmitCipherUpdateCommand
{
    /// <summary>
    /// Writes a daemon's rotated secret back to the cipher via the atomic write-capability check, then pushes a
    /// resync. Throws <see cref="Bit.Core.Exceptions.NotFoundException"/> for an unknown attempt id, and
    /// <see cref="Bit.Core.Exceptions.ConflictException"/> if the write capability no longer holds or
    /// <paramref name="lastKnownRevisionDate"/> is stale.
    /// </summary>
    Task SubmitAsync(Guid daemonId, Guid attemptId, string cipherDataJson, DateTime lastKnownRevisionDate);
}
