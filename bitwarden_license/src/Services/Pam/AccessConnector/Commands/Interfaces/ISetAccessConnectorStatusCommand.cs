namespace Bit.Services.Pam.AccessConnector.Commands.Interfaces;

public interface ISetAccessConnectorStatusCommand
{
    /// <summary>
    /// Enables or disables an access connector. Disabling is a reversible pause: the access connector keeps its
    /// credential but stops authenticating and stops seeing or claiming jobs. The access connector must not already be
    /// in the requested state. Permanent removal is a separate delete, not a status change.
    /// </summary>
    Task SetStatusAsync(Guid organizationId, Guid actingUserId, Guid accessConnectorId, bool enable);
}
