namespace Bit.Services.Pam.AccessConnector.Commands.Interfaces;

public interface IDeleteRotationConfigCommand
{
    /// <summary>
    /// Deletes a rotation config with its jobs and attempts (spec <c>DeleteRotationConfig</c>), refused while it has
    /// an active job. The audit trail keeps the history.
    /// </summary>
    Task DeleteAsync(Guid organizationId, Guid actingUserId, Guid configId);
}
