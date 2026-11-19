namespace Bit.Services.Pam.AccessConnector.Commands.Interfaces;

public interface IDeleteAccessConnectorCommand
{
    /// <summary>
    /// Permanently deletes an access connector, its target assignments and its <c>dbo.ApiKey</c> credential, releasing
    /// its claimed jobs in the same transaction.
    /// </summary>
    Task DeleteAsync(Guid organizationId, Guid actingUserId, Guid accessConnectorId);
}
