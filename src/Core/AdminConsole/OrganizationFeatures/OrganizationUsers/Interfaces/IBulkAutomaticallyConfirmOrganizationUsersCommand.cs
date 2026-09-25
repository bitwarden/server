using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AutoConfirmUser;
using Bit.Core.AdminConsole.Utilities.v2.Results;

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
    /// Automatically confirms each of the users specified in <paramref name="request"/>.
    /// </summary>
    /// <param name="request">
    /// The bulk confirmation request containing shared context (organization, collection name, actor)
    /// and the per-user entries (organization user ID + encrypted key).
    /// </param>
    Task<IEnumerable<BulkCommandResult>> RunAsync(BulkAutomaticallyConfirmOrganizationUsersRequest request);
}
