namespace Bit.Services.Pam.AccessConnector.Commands.Interfaces;

public interface ISetAccessConnectorStatusCommand
{
    /// <summary>
    /// Enables or disables an access connector. A disabled access connector keeps its credential but can neither
    /// authenticate nor see or claim jobs.
    /// </summary>
    Task SetStatusAsync(Guid organizationId, Guid actingUserId, Guid accessConnectorId, bool enable);
}
