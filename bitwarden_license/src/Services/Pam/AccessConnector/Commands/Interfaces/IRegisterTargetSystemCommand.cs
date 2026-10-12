using Bit.Pam.Entities;
using Bit.Pam.Enums;

namespace Bit.Services.Pam.AccessConnector.Commands.Interfaces;

public interface IRegisterTargetSystemCommand
{
    /// <summary>
    /// Registers a target system's first phase (spec <c>RegisterTargetSystem</c>): name and method only. The row is
    /// created <see cref="PamTargetSystemStatus.PendingSeal"/>; the client's second call seals the security-relevant
    /// fields via a <c>PUT</c>, which is the only transition out of that status.
    /// </summary>
    Task<PamTargetSystem> RegisterAsync(
        Guid organizationId,
        Guid actingUserId,
        string name,
        PamTargetSystemMethod method);
}
