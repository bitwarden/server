using Bit.Pam.Entities;
using Bit.Pam.Enums;

namespace Bit.Services.Pam.AccessConnector.Commands.Interfaces;

public interface IRegisterTargetSystemCommand
{
    /// <summary>
    /// Registers a target system's first phase: name and method only; a second <c>PUT</c> call seals it.
    /// </summary>
    Task<PamTargetSystem> RegisterAsync(
        Guid organizationId,
        Guid actingUserId,
        string name,
        PamTargetSystemMethod method);
}
