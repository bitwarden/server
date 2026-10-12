using Bit.Pam.Entities;

namespace Bit.Services.Pam.AccessConnector.Commands.Interfaces;

public interface ICreateRotationConfigCommand
{
    /// <summary>
    /// Creates a rotation config for a cipher (invariant <c>OneConfigPerCipher</c>). Allowed against a
    /// <see cref="Bit.Pam.Enums.PamTargetSystemStatus.PendingSeal"/> target; refused against a
    /// <see cref="Bit.Pam.Enums.PamTargetSystemStatus.Disabled"/> one.
    /// </summary>
    Task<PamRotationConfig> CreateAsync(
        Guid organizationId,
        Guid actingUserId,
        Guid cipherId,
        Guid targetSystemId,
        string sealedData,
        string? scheduleCron,
        bool rotateOnAccessEnd);
}
