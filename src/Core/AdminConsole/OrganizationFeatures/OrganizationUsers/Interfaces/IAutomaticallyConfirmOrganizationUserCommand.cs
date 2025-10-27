using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AutoConfirmUser;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.DeleteClaimedAccount;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;

/// <summary>
/// Command to automatically confirm an organization user.
/// </summary>
public interface IAutomaticallyConfirmOrganizationUserCommand
{
    /// <summary>
    /// Automatically confirms the organization user based on the provided request data.
    /// </summary>
    /// <param name="request">The request containing necessary information to confirm the organization user.</param>
    /// <returns>
    /// The result of the command. If there was an error, result will contain a typed error describing the problem that
    /// occurred.
    /// </returns>
    Task<CommandResult> AutomaticallyConfirmOrganizationUserAsync(AutomaticallyConfirmOrganizationUserRequest request);
}
