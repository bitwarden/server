using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AutoConfirmUser;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;

/// <summary>
/// Command to bulk automatically confirm multiple organization users in a single operation.
/// </summary>
/// <remarks>
/// This command is used by the bulk auto-confirm on login sweep. When an administrator
/// logs in or unlocks their vault, this command is used to confirm all organization users
/// that are in the <c>Accepted</c> state but were not confirmed while the admin was offline.
/// </remarks>
public interface IBulkAutomaticallyConfirmOrganizationUsersCommand
{
    /// <summary>
    /// Automatically confirms each of the provided organization users.
    /// </summary>
    /// <param name="requests">The collection of auto-confirm requests, one per user to confirm.</param>
    /// <returns>
    /// A list of tuples containing the organization user ID and an optional error message.
    /// A null error indicates a successful confirmation for that user.
    /// </returns>
    Task<IReadOnlyList<(Guid OrganizationUserId, string? Error)>> BulkAutomaticallyConfirmOrganizationUsersAsync(
        IEnumerable<AutomaticallyConfirmOrganizationUserRequest> requests);
}
