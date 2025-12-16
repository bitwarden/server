using Bit.Core.AdminConsole.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.OrganizationConfirmation;

#nullable enable

public interface ISendOrganizationConfirmationCommand
{
    /// <summary>
    /// Sends an organization confirmation email to the specified user.
    /// </summary>
    /// <param name="organization">The organization to send the confirmation email for.</param>
    /// <param name="userEmail">The email address of the user to send the confirmation to.</param>
    Task SendConfirmationAsync(Organization organization, string userEmail);

    /// <summary>
    /// Sends organization confirmation emails to multiple users.
    /// </summary>
    /// <param name="organization">The organization to send the confirmation emails for.</param>
    /// <param name="userEmails">The email addresses of the users to send confirmations to.</param>
    Task SendConfirmationsAsync(Organization organization, IEnumerable<string> userEmails);
}
