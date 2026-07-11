namespace Bit.Services.Pam.Rotation.Commands.Interfaces;

public interface IUnassignDaemonFromTargetCommand
{
    /// <summary>Removes a daemon's assignment to a target system. Guard: the assignment must exist.</summary>
    Task UnassignAsync(Guid organizationId, Guid actingUserId, Guid daemonId, Guid targetSystemId);
}
