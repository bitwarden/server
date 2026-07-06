using Bit.Pam.Entities;

namespace Bit.Services.Pam.Rotation.Commands.Interfaces;

public interface ICreateRotationConfigCommand
{
    /// <summary>
    /// Creates a rotation config for a cipher (spec <c>CreateRotationConfig</c>). Guards: the target system exists,
    /// belongs to the organization, and is <see cref="Bit.Pam.Enums.PamTargetSystemStatus.Active"/>; the cipher
    /// belongs to the organization and has no existing config (invariant <c>OneConfigPerCipher</c>);
    /// <paramref name="terminateSessions"/> may only be true on an automatic target that supports it; the schedule
    /// is parseable and respects the interval floor. Effects: <c>NextRotationAt</c> is computed from
    /// <paramref name="scheduleCron"/> and the config starts enabled.
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
