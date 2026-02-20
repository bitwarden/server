using System.Net;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Mail.Mailer.OrganizationUserAutoConfirmation;
using Bit.Core.Platform.Mail.Mailer;
using Bit.Core.Settings;
using Microsoft.Extensions.Logging;
using OneOf.Types;
using CommandResult = Bit.Core.AdminConsole.Utilities.v2.Results.CommandResult;
using Error = Bit.Core.AdminConsole.Utilities.v2.Error;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AutoConfirmUser;

public record OrganizationAutoConfirmEnabledNotificationRequest(Organization Organization, ICollection<string> Emails);

public record NoEmailsWereProvided() : Error("No emails were provided");

public record EmailSendingFailed() : Error("Failed to send email to organization admins");

public interface IOrganizationAutoConfirmEnabledNotificationCommand
{
    Task<CommandResult> SendEmailAsync(OrganizationAutoConfirmEnabledNotificationRequest request);
}

public class OrganizationAutoConfirmEnabledNotificationCommand(
    IMailer mailer,
    ILogger<OrganizationAutoConfirmEnabledNotificationCommand> logger,
    GlobalSettings globalSettings) : IOrganizationAutoConfirmEnabledNotificationCommand
{
    public async Task<CommandResult> SendEmailAsync(OrganizationAutoConfirmEnabledNotificationRequest request)
    {
        if (request.Emails.Count == 0)
        {
            return new NoEmailsWereProvided();
        }

        var mail = new OrganizationAutoConfirmationEnabled
        {
            ToEmails = request.Emails,
            View = new OrganizationAutoConfirmationEnabledView
            {
                WebVaultUrl = globalSettings.BaseServiceUri.Vault + "#/organizations/" + request.Organization.Id + "/settings/policies"
            },
            Subject = $"Automatic user confirmation is available for {WebUtility.HtmlEncode(request.Organization.Name)}"
        };

        try
        {
            await mailer.SendEmail(mail);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to send email to organization admins for Auto Confirm feature enablement. Organization: {OrganizationId}",
                request.Organization.Id);

            return new EmailSendingFailed();
        }

        return new None();
    }
}
