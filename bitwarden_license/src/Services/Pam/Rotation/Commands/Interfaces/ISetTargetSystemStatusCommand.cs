namespace Bit.Services.Pam.Rotation.Commands.Interfaces;

public interface ISetTargetSystemStatusCommand
{
    /// <summary>
    /// Enables or disables a target system (spec <c>EnableTargetSystem</c> / <c>DisableTargetSystem</c>). Guard: the
    /// target's current status must be the opposite of the requested one.
    /// </summary>
    Task SetStatusAsync(Guid organizationId, Guid actingUserId, Guid targetSystemId, bool enable);
}
