namespace Bit.Services.Pam.AccessConnector.Commands.Interfaces;

public interface IDeleteAccessConnectorCommand
{
    /// <summary>
    /// Permanently deletes an access connector: removes its target assignments, the access connector row, and its
    /// <c>dbo.ApiKey</c> credential. Unlike disable, this is not reversible.
    /// </summary>
    Task DeleteAsync(Guid organizationId, Guid actingUserId, Guid accessConnectorId);
}
