using Bit.Pam.Entities;

namespace Bit.Services.Pam.AccessConnector.Commands.Interfaces;

public interface IUpdateRotationSettingsCommand
{
    /// <summary>
    /// Updates a rotation config's schedule and access-end trigger (spec <c>UpdateRotationSettings</c>), recomputing
    /// <c>NextRotationAt</c> from <paramref name="scheduleCron"/>; a null cron clears it.
    /// </summary>
    Task<PamRotationConfig> UpdateAsync(
        Guid organizationId, Guid actingUserId, Guid configId, string? scheduleCron, bool rotateOnAccessEnd);
}
