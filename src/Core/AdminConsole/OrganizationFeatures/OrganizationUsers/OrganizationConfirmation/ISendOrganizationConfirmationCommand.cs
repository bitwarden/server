using Bit.Core.AdminConsole.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.OrganizationConfirmation;

public interface ISendOrganizationConfirmationCommand
{
    /// <summary>
    /// Sends an organization confirmation email to the specified user.
    /// </summary>
    /// <param name="organization">The organization to send the confirmation email for.</param>
    /// <param name="userEmail">The email address of the user to send the confirmation to.</param>
    /// <param name="accessSecretsManager">Whether the user has access to Secrets Manager.</param>
    Task SendConfirmationAsync(Organization organization, string userEmail, bool accessSecretsManager);
}
