namespace Bit.Services.Pam.Rotation.Commands.Interfaces;

public interface IRevokeDaemonCommand
{
    /// <summary>
    /// Revokes a rotation daemon (spec <c>RevokeDaemon</c>): flips its status to Revoked and deletes its
    /// <c>dbo.ApiKey</c> credential row, closing the credential the way Secrets Manager revokes an access token.
    /// Guard: the daemon must currently be Enrolled.
    /// </summary>
    Task RevokeAsync(Guid organizationId, Guid actingUserId, Guid daemonId);
}
