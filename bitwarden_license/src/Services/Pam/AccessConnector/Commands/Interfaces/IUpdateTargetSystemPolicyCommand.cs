using Bit.Pam.Models;

namespace Bit.Services.Pam.AccessConnector.Commands.Interfaces;

public interface IUpdateTargetSystemPolicyCommand
{
    /// <summary>
    /// Updates a target system's password policy, and an automatic target's session-termination capability.
    /// Guards: <paramref name="supportsSessionTermination"/> is required for an
    /// <see cref="Bit.Pam.Enums.PamTargetSystemMethod.Automatic"/> target and must be null for a manual one, and
    /// can only be withdrawn if no rotation config on the target has
    /// <see cref="Bit.Pam.Entities.PamRotationConfig.TerminateSessions"/> set.
    /// </summary>
    Task UpdateAsync(
        Guid organizationId, Guid actingUserId, Guid targetSystemId, PamPasswordPolicy passwordPolicy,
        bool? supportsSessionTermination);
}
