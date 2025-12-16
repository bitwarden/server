using System.Net;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Mail.Mailer.OrganizationConfirmation;
using Bit.Core.Billing.Enums;
using Bit.Core.Platform.Mail.Mailer;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.OrganizationConfirmation;

public class SendOrganizationConfirmationCommand(IMailer mailer) : ISendOrganizationConfirmationCommand
{
    public async Task SendConfirmationAsync(Organization organization, string userEmail)
    {
        await SendConfirmationsAsync(organization, [userEmail]);
    }

    public async Task SendConfirmationsAsync(Organization organization, IEnumerable<string> userEmails)
    {
        var userEmailsList = userEmails.ToList();
        if (userEmailsList.Count == 0)
        {
            return;
        }

        var organizationName = WebUtility.HtmlDecode(organization.Name);

        if (IsEnterpriseOrTeamsPlan(organization.PlanType))
        {
            await SendEnterpriseTeamsEmailsAsync(userEmailsList, organizationName);
            return;
        }

        await SendFamilyFreeConfirmEmailsAsync(userEmailsList, organizationName);
    }

    private async Task SendEnterpriseTeamsEmailsAsync(List<string> userEmailsList, string organizationName)
    {
        var mail = new OrganizationConfirmationEnterpriseTeams
        {
            ToEmails = userEmailsList,
            View = new OrganizationConfirmationEnterpriseTeamsView
            {
                OrganizationName = organizationName
            }
        };

        await mailer.SendEmail(mail);
    }

    private async Task SendFamilyFreeConfirmEmailsAsync(List<string> userEmailsList, string organizationName)
    {
        var mail = new OrganizationConfirmationFamilyFree
        {
            ToEmails = userEmailsList,
            View = new OrganizationConfirmationFamilyFreeView
            {
                OrganizationName = organizationName
            }
        };

        await mailer.SendEmail(mail);
    }


    private static bool IsEnterpriseOrTeamsPlan(PlanType planType)
    {
        return planType switch
        {
            PlanType.TeamsMonthly2019 or
            PlanType.TeamsAnnually2019 or
            PlanType.TeamsMonthly2020 or
            PlanType.TeamsAnnually2020 or
            PlanType.TeamsMonthly2023 or
            PlanType.TeamsAnnually2023 or
            PlanType.TeamsStarter2023 or
            PlanType.TeamsMonthly or
            PlanType.TeamsAnnually or
            PlanType.TeamsStarter or
            PlanType.EnterpriseMonthly2019 or
            PlanType.EnterpriseAnnually2019 or
            PlanType.EnterpriseMonthly2020 or
            PlanType.EnterpriseAnnually2020 or
            PlanType.EnterpriseMonthly2023 or
            PlanType.EnterpriseAnnually2023 or
            PlanType.EnterpriseMonthly or
            PlanType.EnterpriseAnnually => true,
            _ => false
        };
    }
}
