namespace Bit.Services.Pam.AccessConnector.Commands.Interfaces;

public interface IUnassignAccessConnectorFromTargetCommand
{
    /// <summary>Removes an access connector's assignment to a target system. Guard: the assignment must
    /// exist.</summary>
    Task UnassignAsync(Guid organizationId, Guid actingUserId, Guid accessConnectorId, Guid targetSystemId);
}
