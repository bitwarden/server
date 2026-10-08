namespace Bit.Services.Pam.AccessConnector.Commands.Interfaces;

public interface ISetTargetSystemStatusCommand
{
    /// <summary>
    /// Enables or disables a target system (spec <c>EnableTargetSystem</c> / <c>DisableTargetSystem</c>).
    /// </summary>
    Task SetStatusAsync(Guid organizationId, Guid actingUserId, Guid targetSystemId, bool enable);
}
