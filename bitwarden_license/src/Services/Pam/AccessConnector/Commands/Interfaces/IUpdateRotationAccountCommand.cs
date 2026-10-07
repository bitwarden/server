using Bit.Pam.Entities;

namespace Bit.Services.Pam.AccessConnector.Commands.Interfaces;

public interface IUpdateRotationAccountCommand
{
    /// <summary>
    /// Updates the account a rotation config rotates and its session-termination setting (spec
    /// <c>UpdateRotationAccount</c>), refused while the config has an active job. <paramref name="terminateSessions"/>
    /// is guarded as on create.
    /// </summary>
    Task<PamRotationConfig> UpdateAsync(
        Guid organizationId, Guid actingUserId, Guid configId, string accountIdentity, bool terminateSessions);
}
