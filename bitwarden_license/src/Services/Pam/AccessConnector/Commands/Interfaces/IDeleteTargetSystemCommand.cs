namespace Bit.Services.Pam.AccessConnector.Commands.Interfaces;

public interface IDeleteTargetSystemCommand
{
    /// <summary>
    /// Permanently deletes a target system, cascading the access connector assignments that point at it.
    /// Guard: no rotation config may still name the target. Unlike disable, this is not reversible.
    /// </summary>
    Task DeleteAsync(Guid organizationId, Guid actingUserId, Guid targetSystemId);
}
