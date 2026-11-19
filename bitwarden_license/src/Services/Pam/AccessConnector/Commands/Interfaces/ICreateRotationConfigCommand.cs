using Bit.Pam.Entities;

namespace Bit.Services.Pam.AccessConnector.Commands.Interfaces;

public interface ICreateRotationConfigCommand
{
    /// <summary>
    /// Creates a rotation config for a cipher (invariant <c>OneConfigPerCipher</c>); refused against a Disabled target.
    /// </summary>
    Task<PamRotationConfig> CreateAsync(
        Guid organizationId,
        Guid actingUserId,
        Guid cipherId,
        Guid targetSystemId,
        string sealedData,
        string wrappedKey,
        string? scheduleCron,
        bool rotateOnAccessEnd);
}
