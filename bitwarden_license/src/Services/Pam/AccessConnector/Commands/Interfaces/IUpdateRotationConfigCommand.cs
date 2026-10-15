using Bit.Pam.Entities;

namespace Bit.Services.Pam.AccessConnector.Commands.Interfaces;

public interface IUpdateRotationConfigCommand
{
    /// <summary>
    /// Writes a rotation config's sealed blob and wrapped key, schedule and access-end trigger, recomputing
    /// <c>NextRotationDate</c> from <paramref name="scheduleCron"/>; a null cron clears it. A changed blob or key is
    /// refused while the config has an active job.
    /// </summary>
    Task<PamRotationConfig> UpdateAsync(
        Guid organizationId,
        Guid actingUserId,
        Guid configId,
        string sealedData,
        string wrappedKey,
        string? scheduleCron,
        bool rotateOnAccessEnd);
}
