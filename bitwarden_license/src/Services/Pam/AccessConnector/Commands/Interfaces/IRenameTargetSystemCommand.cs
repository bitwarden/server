namespace Bit.Services.Pam.AccessConnector.Commands.Interfaces;

public interface IRenameTargetSystemCommand
{
    /// <summary>Renames a target system. The access connector keys its config by id, so this is display-only.</summary>
    Task RenameAsync(Guid organizationId, Guid actingUserId, Guid targetSystemId, string name);
}
