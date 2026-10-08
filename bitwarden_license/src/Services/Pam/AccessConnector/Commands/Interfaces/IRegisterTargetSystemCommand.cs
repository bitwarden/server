using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Models;

namespace Bit.Services.Pam.AccessConnector.Commands.Interfaces;

public interface IRegisterTargetSystemCommand
{
    /// <summary>
    /// Registers a target system (spec <c>RegisterAutomaticTargetSystem</c> / <c>RegisterManualTargetSystem</c>). An
    /// automatic target requires <paramref name="kind"/>, <paramref name="passwordPolicy"/> and
    /// <paramref name="supportsSessionTermination"/>; a manual target takes at most a policy, as operator guidance.
    /// </summary>
    Task<PamTargetSystem> RegisterAsync(
        Guid organizationId,
        Guid actingUserId,
        string name,
        PamTargetSystemMethod method,
        PamTargetSystemKind? kind,
        PamPasswordPolicy? passwordPolicy,
        bool? supportsSessionTermination);
}
