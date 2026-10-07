using Bit.Pam.Entities;

namespace Bit.Services.Pam.AccessConnector.Commands.Interfaces;

public interface ICreateRotationConfigCommand
{
    /// <summary>
    /// Creates a rotation config for a cipher on an <see cref="Bit.Pam.Enums.PamTargetSystemStatus.Active"/> target
    /// (invariant <c>OneConfigPerCipher</c>). <paramref name="terminateSessions"/> requires an automatic target that
    /// supports it.
    /// </summary>
    Task<PamRotationConfig> CreateAsync(
        Guid organizationId,
        Guid actingUserId,
        Guid cipherId,
        Guid targetSystemId,
        string accountIdentity,
        bool terminateSessions,
        string? scheduleCron,
        bool rotateOnAccessEnd);
}
