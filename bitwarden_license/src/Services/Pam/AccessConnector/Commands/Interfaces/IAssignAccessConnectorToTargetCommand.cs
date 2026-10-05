namespace Bit.Services.Pam.AccessConnector.Commands.Interfaces;

public interface IAssignAccessConnectorToTargetCommand
{
    /// <summary>
    /// Assigns an access connector to a target system (invariant <c>OneAssignmentPerConnectorTarget</c>). Guards: the
    /// access connector must be Enrolled; the target must be an
    /// <see cref="Bit.Pam.Enums.PamTargetSystemMethod.Automatic"/> target; no assignment may already exist for the
    /// pair.
    /// </summary>
    Task AssignAsync(Guid organizationId, Guid actingUserId, Guid accessConnectorId, Guid targetSystemId);
}
