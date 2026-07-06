using Bit.Pam.Entities;

namespace Bit.Services.Pam.Rotation.Commands.Interfaces;

public interface IUpdateRotationAccountCommand
{
    /// <summary>
    /// Updates the account a rotation config rotates and its termination setting (spec
    /// <c>UpdateRotationAccount</c>). Guards: the config must have no active job; the same termination-capability
    /// guard as create (<paramref name="terminateSessions"/> may only be true on an automatic target that supports
    /// it).
    /// </summary>
    Task<PamRotationConfig> UpdateAsync(
        Guid organizationId, Guid actingUserId, Guid configId, string accountIdentity, bool terminateSessions);
}
