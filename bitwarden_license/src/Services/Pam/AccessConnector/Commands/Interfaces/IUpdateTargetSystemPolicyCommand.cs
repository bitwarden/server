using Bit.Pam.Models;

namespace Bit.Services.Pam.AccessConnector.Commands.Interfaces;

public interface IUpdateTargetSystemPolicyCommand
{
    /// <summary>
    /// Updates a target system's password policy and, for an automatic target, its session-termination capability.
    /// The capability cannot be withdrawn while a config on the target terminates sessions.
    /// </summary>
    Task UpdateAsync(
        Guid organizationId, Guid actingUserId, Guid targetSystemId, PamPasswordPolicy passwordPolicy,
        bool? supportsSessionTermination);
}
