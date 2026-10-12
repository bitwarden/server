using Bit.Pam.Entities;

namespace Bit.Services.Pam.AccessConnector.Commands.Interfaces;

public interface ISealRotationConfigCommand
{
    /// <summary>
    /// Stores a rotation config's sealed blob verbatim, replacing any previous one (spec <c>SealRotationConfig</c>),
    /// refused while the config has an active job.
    /// </summary>
    Task<PamRotationConfig> SealAsync(Guid organizationId, Guid actingUserId, Guid configId, string sealedData);
}
