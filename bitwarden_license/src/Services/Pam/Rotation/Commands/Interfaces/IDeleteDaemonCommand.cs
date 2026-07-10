namespace Bit.Services.Pam.Rotation.Commands.Interfaces;

public interface IDeleteDaemonCommand
{
    /// <summary>
    /// Permanently deletes a rotation daemon: removes its target assignments and the daemon row, then deletes its
    /// <c>dbo.ApiKey</c> credential (invalidating it the way Secrets Manager revokes an access token). Unlike disable,
    /// this is not reversible — re-registering mints a new daemon and credential. Any rotation jobs the daemon had
    /// claimed are released by the sweep once it stops heartbeating.
    /// </summary>
    Task DeleteAsync(Guid organizationId, Guid actingUserId, Guid daemonId);
}
