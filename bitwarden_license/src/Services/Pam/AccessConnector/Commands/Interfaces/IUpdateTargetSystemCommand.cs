namespace Bit.Services.Pam.AccessConnector.Commands.Interfaces;

public interface IUpdateTargetSystemCommand
{
    /// <summary>Writes the target's name and sealed blob; moves PendingSeal to Active, leaves Disabled alone.</summary>
    Task UpdateAsync(Guid organizationId, Guid actingUserId, Guid targetSystemId, string name, string sealedData);
}
