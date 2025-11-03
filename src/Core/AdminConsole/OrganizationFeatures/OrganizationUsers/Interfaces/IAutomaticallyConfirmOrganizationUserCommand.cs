using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AutoConfirmUser;
using Bit.Core.AdminConsole.Utilities.v2.Results;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;

/// <summary>
/// Command to automatically confirm an organization user.
/// </summary>
/// <remarks>
/// The auto-confirm feature enables eligible client apps to confirm OrganizationUsers
/// automatically via push notifications, eliminating the need for manual administrator
/// intervention. Client apps receive a push notification, perform the required key exchange,
/// and submit an auto-confirm request to the server. This command processes those
/// client-initiated requests and should only be used in that specific context.
/// </remarks>
public interface IAutomaticallyConfirmOrganizationUserCommand
{
    /// <summary>
    /// Automatically confirms the organization user based on the provided request data.
    /// </summary>
    /// <param name="request">The request containing necessary information to confirm the organization user.</param>
    /// <returns>
    /// The result of the command. If there was an error, the result will contain a typed error describing the problem
    /// that occurred.
    /// </returns>
    Task<CommandResult> AutomaticallyConfirmOrganizationUserAsync(AutomaticallyConfirmOrganizationUserRequest request);
}
