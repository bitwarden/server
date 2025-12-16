using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Mail.Mailer.OrganizationConfirmation;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.OrganizationConfirmation;
using Bit.Core.Billing.Enums;
using Bit.Core.Platform.Mail.Mailer;
using Bit.Core.Test.AutoFixture.OrganizationFixtures;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationUsers.OrganizationConfirmation;

[SutProviderCustomize]
public class SendOrganizationConfirmationCommandTests
{
    [Theory]
    [OrganizationCustomize, BitAutoData]
    public async Task SendConfirmationAsync_EnterpriseOrganization_SendsEnterpriseTeamsEmail(
        Organization organization,
        string userEmail,
        SutProvider<SendOrganizationConfirmationCommand> sutProvider)
    {
        // Arrange
        organization.PlanType = PlanType.EnterpriseAnnually;
        organization.Name = "Test Enterprise Org";

        // Act
        await sutProvider.Sut.SendConfirmationAsync(organization, userEmail);

        // Assert
        await sutProvider.GetDependency<IMailer>().Received(1)
            .SendEmail(Arg.Is<OrganizationConfirmationEnterpriseTeams>(mail =>
                mail.ToEmails.Contains(userEmail) &&
                mail.ToEmails.Count() == 1 &&
                mail.View.OrganizationName == organization.Name &&
                mail.Subject == "Welcome to Bitwarden!"));
    }

    [Theory]
    [OrganizationCustomize, BitAutoData]
    public async Task SendConfirmationAsync_TeamsOrganization_SendsEnterpriseTeamsEmail(
        Organization organization,
        string userEmail,
        SutProvider<SendOrganizationConfirmationCommand> sutProvider)
    {
        // Arrange
        organization.PlanType = PlanType.TeamsAnnually;
        organization.Name = "Test Teams Org";

        // Act
        await sutProvider.Sut.SendConfirmationAsync(organization, userEmail);

        // Assert
        await sutProvider.GetDependency<IMailer>().Received(1)
            .SendEmail(Arg.Is<OrganizationConfirmationEnterpriseTeams>(mail =>
                mail.ToEmails.Contains(userEmail) &&
                mail.ToEmails.Count() == 1 &&
                mail.View.OrganizationName == organization.Name &&
                mail.Subject == "Welcome to Bitwarden!"));
    }

    [Theory]
    [OrganizationCustomize, BitAutoData]
    public async Task SendConfirmationAsync_FamilyOrganization_SendsFamilyFreeEmail(
        Organization organization,
        string userEmail,
        SutProvider<SendOrganizationConfirmationCommand> sutProvider)
    {
        // Arrange
        organization.PlanType = PlanType.FamiliesAnnually;
        organization.Name = "Test Family Org";

        // Act
        await sutProvider.Sut.SendConfirmationAsync(organization, userEmail);

        // Assert
        await sutProvider.GetDependency<IMailer>().Received(1)
            .SendEmail(Arg.Is<OrganizationConfirmationFamilyFree>(mail =>
                mail.ToEmails.Contains(userEmail) &&
                mail.ToEmails.Count() == 1 &&
                mail.View.OrganizationName == organization.Name &&
                mail.Subject == "Welcome to Bitwarden!"));
    }

    [Theory]
    [OrganizationCustomize, BitAutoData]
    public async Task SendConfirmationAsync_FreeOrganization_SendsFamilyFreeEmail(
        Organization organization,
        string userEmail,
        SutProvider<SendOrganizationConfirmationCommand> sutProvider)
    {
        // Arrange
        organization.PlanType = PlanType.Free;
        organization.Name = "Test Free Org";

        // Act
        await sutProvider.Sut.SendConfirmationAsync(organization, userEmail);

        // Assert
        await sutProvider.GetDependency<IMailer>().Received(1)
            .SendEmail(Arg.Is<OrganizationConfirmationFamilyFree>(mail =>
                mail.ToEmails.Contains(userEmail) &&
                mail.ToEmails.Count() == 1 &&
                mail.View.OrganizationName == organization.Name &&
                mail.Subject == "Welcome to Bitwarden!"));
    }

    [Theory]
    [OrganizationCustomize, BitAutoData]
    public async Task SendConfirmationsAsync_MultipleUsers_SendsSingleEmail(
        Organization organization,
        List<string> userEmails,
        SutProvider<SendOrganizationConfirmationCommand> sutProvider)
    {
        // Arrange
        organization.PlanType = PlanType.EnterpriseAnnually;
        organization.Name = "Test Enterprise Org";

        // Act
        await sutProvider.Sut.SendConfirmationsAsync(organization, userEmails);

        // Assert
        await sutProvider.GetDependency<IMailer>().Received(1)
            .SendEmail(Arg.Is<OrganizationConfirmationEnterpriseTeams>(mail =>
                mail.ToEmails.SequenceEqual(userEmails) &&
                mail.View.OrganizationName == organization.Name));
    }

    [Theory]
    [OrganizationCustomize, BitAutoData]
    public async Task SendConfirmationsAsync_EmptyUserList_DoesNotSendEmail(
        Organization organization,
        SutProvider<SendOrganizationConfirmationCommand> sutProvider)
    {
        // Arrange
        organization.PlanType = PlanType.EnterpriseAnnually;
        organization.Name = "Test Enterprise Org";

        // Act
        await sutProvider.Sut.SendConfirmationsAsync(organization, []);

        // Assert
        await sutProvider.GetDependency<IMailer>().DidNotReceive()
            .SendEmail(Arg.Any<OrganizationConfirmationEnterpriseTeams>());
        await sutProvider.GetDependency<IMailer>().DidNotReceive()
            .SendEmail(Arg.Any<OrganizationConfirmationFamilyFree>());
    }

    [Theory]
    [OrganizationCustomize, BitAutoData]
    public async Task SendConfirmationAsync_HtmlEncodedOrganizationName_DecodesNameCorrectly(
        Organization organization,
        string userEmail,
        SutProvider<SendOrganizationConfirmationCommand> sutProvider)
    {
        // Arrange
        organization.PlanType = PlanType.EnterpriseAnnually;
        organization.Name = "Test &amp; Company";
        var expectedDecodedName = "Test & Company";

        // Act
        await sutProvider.Sut.SendConfirmationAsync(organization, userEmail);

        // Assert
        await sutProvider.GetDependency<IMailer>().Received(1)
            .SendEmail(Arg.Is<OrganizationConfirmationEnterpriseTeams>(mail =>
                mail.View.OrganizationName == expectedDecodedName));
    }

    [Theory]
    [OrganizationCustomize, BitAutoData]
    public async Task SendConfirmationAsync_AllEnterpriseTeamsPlanTypes_SendsEnterpriseTeamsEmail(
        Organization organization,
        string userEmail,
        SutProvider<SendOrganizationConfirmationCommand> sutProvider)
    {
        // Test all Enterprise and Teams plan types
        var enterpriseTeamsPlanTypes = new[]
        {
            PlanType.TeamsMonthly2019, PlanType.TeamsAnnually2019,
            PlanType.TeamsMonthly2020, PlanType.TeamsAnnually2020,
            PlanType.TeamsMonthly2023, PlanType.TeamsAnnually2023,
            PlanType.TeamsStarter2023, PlanType.TeamsMonthly,
            PlanType.TeamsAnnually, PlanType.TeamsStarter,
            PlanType.EnterpriseMonthly2019, PlanType.EnterpriseAnnually2019,
            PlanType.EnterpriseMonthly2020, PlanType.EnterpriseAnnually2020,
            PlanType.EnterpriseMonthly2023, PlanType.EnterpriseAnnually2023,
            PlanType.EnterpriseMonthly, PlanType.EnterpriseAnnually
        };

        foreach (var planType in enterpriseTeamsPlanTypes)
        {
            // Arrange
            organization.PlanType = planType;
            organization.Name = "Test Org";
            sutProvider.GetDependency<IMailer>().ClearReceivedCalls();

            // Act
            await sutProvider.Sut.SendConfirmationAsync(organization, userEmail);

            // Assert
            await sutProvider.GetDependency<IMailer>().Received(1)
                .SendEmail(Arg.Any<OrganizationConfirmationEnterpriseTeams>());
            await sutProvider.GetDependency<IMailer>().DidNotReceive()
                .SendEmail(Arg.Any<OrganizationConfirmationFamilyFree>());
        }
    }

    [Theory]
    [OrganizationCustomize, BitAutoData]
    public async Task SendConfirmationAsync_AllFamilyFreePlanTypes_SendsFamilyFreeEmail(
        Organization organization,
        string userEmail,
        SutProvider<SendOrganizationConfirmationCommand> sutProvider)
    {
        // Test all Family, Free, and Custom plan types
        var familyFreePlanTypes = new[]
        {
            PlanType.Free, PlanType.FamiliesAnnually2019,
            PlanType.FamiliesAnnually2025, PlanType.FamiliesAnnually,
            PlanType.Custom
        };

        foreach (var planType in familyFreePlanTypes)
        {
            // Arrange
            organization.PlanType = planType;
            organization.Name = "Test Org";
            sutProvider.GetDependency<IMailer>().ClearReceivedCalls();

            // Act
            await sutProvider.Sut.SendConfirmationAsync(organization, userEmail);

            // Assert
            await sutProvider.GetDependency<IMailer>().Received(1)
                .SendEmail(Arg.Any<OrganizationConfirmationFamilyFree>());
            await sutProvider.GetDependency<IMailer>().DidNotReceive()
                .SendEmail(Arg.Any<OrganizationConfirmationEnterpriseTeams>());
        }
    }
}
