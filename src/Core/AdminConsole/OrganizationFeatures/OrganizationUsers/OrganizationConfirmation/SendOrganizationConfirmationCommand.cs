using System.Net;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Mail.Mailer.OrganizationConfirmation;
using Bit.Core.Billing.Enums;
using Bit.Core.Platform.Mail.Mailer;
using Bit.Core.Services;
using Bit.Core.Settings;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.OrganizationConfirmation;

public class SendOrganizationConfirmationCommand(
    IMailer mailer,
    GlobalSettings globalSettings,
    IMailService mailService,
    IFeatureService featureService) : ISendOrganizationConfirmationCommand
{
    public async Task SendConfirmationAsync(Organization organization, string userEmail, bool accessSecretsManager = false)
    {
        await SendConfirmationsAsync(organization, [userEmail], accessSecretsManager);
    }

    public async Task SendConfirmationsAsync(Organization organization, IEnumerable<string> userEmails, bool accessSecretsManager = false)
    {
        var userEmailsList = userEmails.ToList();
        if (userEmailsList.Count == 0)
        {
            return;
        }

        // Check feature flag to determine which email service to use
        if (!featureService.IsEnabled(FeatureFlagKeys.OrganizationConfirmationEmail))
        {
            // Use legacy mail service
            foreach (var email in userEmailsList)
            {
                await mailService.SendOrganizationConfirmedEmailAsync(organization.DisplayName(), email, accessSecretsManager);
            }
            return;
        }

        // Use new mailer pattern
        var organizationName = WebUtility.HtmlDecode(organization.Name);

        if (IsEnterpriseOrTeamsPlan(organization.PlanType))
        {
            await SendEnterpriseTeamsEmailsAsync(userEmailsList, organizationName, accessSecretsManager);
            return;
        }

        await SendFamilyFreeConfirmEmailsAsync(userEmailsList, organizationName, accessSecretsManager);
    }

    private async Task SendEnterpriseTeamsEmailsAsync(List<string> userEmailsList, string organizationName, bool accessSecretsManager)
    {
        var mail = new OrganizationConfirmationEnterpriseTeams
        {
            ToEmails = userEmailsList,
            Subject = $"You Have Been Confirmed To {organizationName}",
            View = new OrganizationConfirmationEnterpriseTeamsView
            {
                OrganizationName = organizationName,
                TitleFirst = "You're confirmed as a member of ",
                TitleSecondBold = organizationName,
                TitleThird = "!",
                WebVaultUrl = accessSecretsManager
                    ? globalSettings.BaseServiceUri.VaultWithHashAndSecretManagerProduct
                    : globalSettings.BaseServiceUri.VaultWithHash
            }
        };

        await mailer.SendEmail(mail);
    }

    private async Task SendFamilyFreeConfirmEmailsAsync(List<string> userEmailsList, string organizationName, bool accessSecretsManager)
    {
        var mail = new OrganizationConfirmationFamilyFree
        {
            ToEmails = userEmailsList,
            Subject = $"You Have Been Confirmed To {organizationName}",
            View = new OrganizationConfirmationFamilyFreeView
            {
                OrganizationName = organizationName,
                TitleFirst = "You're confirmed as a member of ",
                TitleSecondBold = organizationName,
                TitleThird = "!",
                WebVaultUrl = accessSecretsManager
                    ? globalSettings.BaseServiceUri.VaultWithHashAndSecretManagerProduct
                    : globalSettings.BaseServiceUri.VaultWithHash
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
