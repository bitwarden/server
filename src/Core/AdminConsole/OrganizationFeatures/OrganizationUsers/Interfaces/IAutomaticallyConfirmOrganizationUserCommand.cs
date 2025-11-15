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
    /// <remarks>
    /// This action has side effects. The side effects are
    /// <ul>
    ///   <li>Creating an event log entry.</li>
    ///   <li>Syncing organization keys with the user.</li>
    ///   <li>Deleting any registered user devices for the organization.</li>
    ///   <li>Sending an email to the confirmed user.</li>
    ///   <li>Creating the default collection if applicable.</li>
    /// </ul>
    ///
    /// Each of these actions is performed independently of each other and not guaranteed to be performed in any order.
    /// Errors will be reported back for the actions that failed in a consolidated error message.
    /// </remarks>
    /// <returns>
    /// The result of the command. If there was an error, the result will contain a typed error describing the problem
    /// that occurred.
    /// </returns>
    Task<CommandResult> AutomaticallyConfirmOrganizationUserAsync(AutomaticallyConfirmOrganizationUserRequest request);
}
