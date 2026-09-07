namespace Bit.Services.Pam.AccessConnector.Commands.Interfaces;

public interface IDeleteAccessConnectorCommand
{
    /// <summary>
    /// Permanently deletes a rotation daemon: removes its target assignments, the daemon row, and its
    /// <c>dbo.ApiKey</c> credential. Unlike disable, this is not reversible.
    /// </summary>
    Task DeleteAsync(Guid organizationId, Guid actingUserId, Guid daemonId);
}
