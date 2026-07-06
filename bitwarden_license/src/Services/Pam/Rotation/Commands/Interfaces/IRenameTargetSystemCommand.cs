namespace Bit.Services.Pam.Rotation.Commands.Interfaces;

public interface IRenameTargetSystemCommand
{
    /// <summary>Renames a target system. Display-only — the id keys the daemon's connector resolver.</summary>
    Task RenameAsync(Guid organizationId, Guid actingUserId, Guid targetSystemId, string name);
}
