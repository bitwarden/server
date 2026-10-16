namespace Bit.Services.Pam.AccessConnector.Commands.Interfaces;

public interface IAssignAccessConnectorToTargetCommand
{
    /// <summary>
    /// Assigns an access connector to a target system (invariant <c>OneAssignmentPerConnectorTarget</c>). Requires an
    /// enabled access connector, an <see cref="Bit.Pam.Enums.PamTargetSystemMethod.Automatic"/> target, and edit
    /// access to every item the target rotates.
    /// </summary>
    Task AssignAsync(Guid organizationId, Guid actingUserId, Guid accessConnectorId, Guid targetSystemId);
}
