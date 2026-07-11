using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Models;

namespace Bit.Services.Pam.Rotation.Commands.Interfaces;

public interface IRegisterTargetSystemCommand
{
    /// <summary>
    /// Registers a new target system, automatic or manual (spec <c>RegisterAutomaticTargetSystem</c> /
    /// <c>RegisterManualTargetSystem</c>). An <see cref="PamTargetSystemMethod.Automatic"/> target requires
    /// <paramref name="kind"/>, <paramref name="passwordPolicy"/>, and <paramref name="supportsSessionTermination"/>;
    /// a <see cref="PamTargetSystemMethod.Manual"/> target requires all three to be null.
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
