namespace Bit.Services.Pam.Rotation.Commands.Interfaces;

public interface ISubmitCipherUpdateCommand
{
    /// <summary>
    /// Writes a daemon's rotated secret back to the cipher (spec <c>AcceptCipherUpdate</c> /
    /// <c>RejectCipherUpdate</c>) via the atomic write-capability check, then pushes a resync. Throws
    /// <see cref="Bit.Core.Exceptions.NotFoundException"/> for an unknown attempt id (no audit) and
    /// <see cref="Bit.Core.Exceptions.ConflictException"/> when the write capability no longer holds or
    /// <paramref name="lastKnownRevisionDate"/> no longer matches the cipher's current revision (a concurrent user
    /// edit won) — both audited as <c>write_rejected</c>.
    /// </summary>
    Task SubmitAsync(Guid daemonId, Guid attemptId, string cipherDataJson, DateTime lastKnownRevisionDate);
}
