using Bit.Pam.Models;

namespace Bit.Services.Pam.Rotation.Commands.Interfaces;

public interface IUpdateTargetSystemPolicyCommand
{
    /// <summary>
    /// Updates an automatic target system's password policy and session-termination capability (spec
    /// <c>UpdateTargetSystemPolicy</c>). Guards: the target must be
    /// <see cref="Bit.Pam.Enums.PamTargetSystemMethod.Automatic"/>; <paramref name="supportsSessionTermination"/> may
    /// only be withdrawn (true to false) when no rotation config on the target has
    /// <see cref="Bit.Pam.Entities.PamRotationConfig.TerminateSessions"/> set.
    /// </summary>
    Task UpdateAsync(
        Guid organizationId, Guid actingUserId, Guid targetSystemId, PamPasswordPolicy passwordPolicy,
        bool supportsSessionTermination);
}
