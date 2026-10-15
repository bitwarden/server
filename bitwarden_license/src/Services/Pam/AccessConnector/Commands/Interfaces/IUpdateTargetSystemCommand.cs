namespace Bit.Services.Pam.AccessConnector.Commands.Interfaces;

public interface IUpdateTargetSystemCommand
{
    /// <summary>Writes the target's name, sealed blob and wrapped key; moves PendingSeal to Active, leaves Disabled alone.</summary>
    Task UpdateAsync(Guid organizationId, Guid actingUserId, Guid targetSystemId, string name, string sealedData, string wrappedKey);
}
