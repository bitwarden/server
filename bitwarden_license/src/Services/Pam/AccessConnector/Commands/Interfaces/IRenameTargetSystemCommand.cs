namespace Bit.Services.Pam.AccessConnector.Commands.Interfaces;

public interface IRenameTargetSystemCommand
{
    /// <summary>Renames a target system. Display-only — the id keys the access connector's connector
    /// resolver.</summary>
    Task RenameAsync(Guid organizationId, Guid actingUserId, Guid targetSystemId, string name);
}
