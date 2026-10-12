using Bit.Pam.Entities;

namespace Bit.Services.Pam.AccessConnector.Commands.Interfaces;

public interface ISealRotationConfigCommand
{
    /// <summary>
    /// Stores the config's sealed blob, replacing any previous one; refused while it has an active job.
    /// </summary>
    Task<PamRotationConfig> SealAsync(Guid organizationId, Guid actingUserId, Guid configId, string sealedData);
}
