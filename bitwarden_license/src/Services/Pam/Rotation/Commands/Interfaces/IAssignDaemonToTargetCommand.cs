namespace Bit.Services.Pam.Rotation.Commands.Interfaces;

public interface IAssignDaemonToTargetCommand
{
    /// <summary>
    /// Assigns a daemon to a target system (invariant <c>OneAssignmentPerDaemonTarget</c>). Guards: the daemon must
    /// be Enrolled; the target must be an <see cref="Bit.Pam.Enums.PamTargetSystemMethod.Automatic"/> target; no
    /// assignment may already exist for the pair.
    /// </summary>
    Task AssignAsync(Guid organizationId, Guid actingUserId, Guid daemonId, Guid targetSystemId);
}
