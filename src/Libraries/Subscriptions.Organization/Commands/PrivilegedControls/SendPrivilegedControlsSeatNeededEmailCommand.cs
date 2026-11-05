using Bit.Core.Billing.Models.Mail.Mailer;
using Bit.Core.Billing.Organizations.Commands;
using Bit.Core.Enums;
using Bit.Core.Platform.Mail.Mailer;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Bitwarden.Server.Sdk.Environment;
using Microsoft.Extensions.Logging;
using OrganizationEntity = Bit.Core.AdminConsole.Entities.Organization;

namespace Bit.Subscriptions.Organization.Commands.PrivilegedControls;

internal sealed class SendPrivilegedControlsSeatNeededEmailCommand(
    IOrganizationUserRepository organizationUserRepository,
    IMailer mailer,
    IGlobalSettings globalSettings,
    IBitwardenEnvironment environment,
    ILogger<SendPrivilegedControlsSeatNeededEmailCommand> logger) : ISendPrivilegedControlsSeatNeededEmailCommand
{
    public async Task RunAsync(OrganizationEntity organization, string memberEmail)
    {
        if (environment.SelfHosted)
        {
            return;
        }

        try
        {
            var ownerEmails = (await organizationUserRepository.GetManyByMinimumRoleAsync(
                    organization.Id, OrganizationUserType.Owner))
                .Select(u => u.Email)
                .Distinct();

            var organizationName = organization.DisplayName();

            await mailer.SendEmail(new PrivilegedControlsSeatNeededMail
            {
                ToEmails = ownerEmails,
                Subject = $"A member at {organizationName} needs a Privileged Controls seat",
                View = new PrivilegedControlsSeatNeededView(globalSettings)
                {
                    OrganizationId = organization.Id,
                    OrganizationName = CoreHelpers.PreventEmailAutoLinking(organizationName),
                    MemberEmail = CoreHelpers.PreventEmailAutoLinking(memberEmail)
                }
            });
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error encountered notifying organization owners that a member needs a Privileged Controls seat.");
        }
    }
}
