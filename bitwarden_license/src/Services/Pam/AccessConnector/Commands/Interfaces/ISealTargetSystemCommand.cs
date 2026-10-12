namespace Bit.Services.Pam.AccessConnector.Commands.Interfaces;

public interface ISealTargetSystemCommand
{
    /// <summary>
    /// Stores a target system's sealed blob verbatim, replacing any previous one (spec <c>SealTargetSystem</c>). If
    /// the target is <see cref="Bit.Pam.Enums.PamTargetSystemStatus.PendingSeal"/> this moves it to
    /// <see cref="Bit.Pam.Enums.PamTargetSystemStatus.Active"/> — the only transition out of that status. A
    /// <see cref="Bit.Pam.Enums.PamTargetSystemStatus.Disabled"/> target stays disabled.
    /// </summary>
    Task SealAsync(Guid organizationId, Guid actingUserId, Guid targetSystemId, string sealedData);
}
