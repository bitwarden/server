using Bit.Pam.Entities;

namespace Bit.Services.Pam.Rotation.Commands.Interfaces;

public interface IUpdateRotationSettingsCommand
{
    /// <summary>
    /// Updates a rotation config's schedule and access-end trigger (spec <c>UpdateRotationSettings</c>). The
    /// schedule is re-validated and <c>NextRotationAt</c> is recomputed from <paramref name="scheduleCron"/>
    /// (recompute-on-edit; a null cron clears it).
    /// </summary>
    Task<PamRotationConfig> UpdateAsync(
        Guid organizationId, Guid actingUserId, Guid configId, string? scheduleCron, bool rotateOnAccessEnd);
}
