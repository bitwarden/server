namespace Bit.Services.Pam.AccessConnector.Commands.Interfaces;

public interface IDeleteTargetSystemCommand
{
    /// <summary>
    /// Permanently deletes a target system and its access connector assignments, refused while a rotation config
    /// names it.
    /// </summary>
    Task DeleteAsync(Guid organizationId, Guid actingUserId, Guid targetSystemId);
}
