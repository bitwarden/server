namespace Bit.Services.Pam.Rotation.Commands.Interfaces;

public interface IDeleteRotationConfigCommand
{
    /// <summary>
    /// Deletes a rotation config, cascading its jobs and attempts (spec <c>DeleteRotationConfig</c>) — the durable
    /// history stays in the audit trail, not the deleted rows. Guard: the config must have no active job.
    /// </summary>
    Task DeleteAsync(Guid organizationId, Guid actingUserId, Guid configId);
}
