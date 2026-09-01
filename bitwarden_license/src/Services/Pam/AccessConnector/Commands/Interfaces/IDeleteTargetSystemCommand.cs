namespace Bit.Services.Pam.AccessConnector.Commands.Interfaces;

public interface IDeleteTargetSystemCommand
{
    /// <summary>
    /// Permanently deletes a target system, cascading the access connector assignments that point at it (spec
    /// <c>DeleteTargetSystem</c>) — an assignment is only the connector-to-target edge, and the durable record of the
    /// target is the audit trail rather than the row. Guard: no rotation config may still name the target; those are
    /// deleted first. Unlike disable, this is not reversible, and it is independent of the target's status — a target
    /// that has left the estate need not be disabled on the way out.
    /// </summary>
    Task DeleteAsync(Guid organizationId, Guid actingUserId, Guid targetSystemId);
}
