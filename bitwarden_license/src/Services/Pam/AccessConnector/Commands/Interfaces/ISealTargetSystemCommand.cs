namespace Bit.Services.Pam.AccessConnector.Commands.Interfaces;

public interface ISealTargetSystemCommand
{
    /// <summary>Stores the target's sealed blob; moves PendingSeal to Active, leaves Disabled alone.</summary>
    Task SealAsync(Guid organizationId, Guid actingUserId, Guid targetSystemId, string sealedData);
}
