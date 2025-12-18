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
    /// <param name="accessSecretsManager">Whether the user has access to Secrets Manager.</param>
    Task SendConfirmationAsync(Organization organization, string userEmail, bool accessSecretsManager = false);

    /// <summary>
    /// Sends organization confirmation emails to multiple users.
    /// </summary>
    /// <param name="organization">The organization to send the confirmation emails for.</param>
    /// <param name="userEmails">The email addresses of the users to send confirmations to.</param>
    /// <param name="accessSecretsManager">Whether the users have access to Secrets Manager.</param>
    Task SendConfirmationsAsync(Organization organization, IEnumerable<string> userEmails, bool accessSecretsManager = false);
}
