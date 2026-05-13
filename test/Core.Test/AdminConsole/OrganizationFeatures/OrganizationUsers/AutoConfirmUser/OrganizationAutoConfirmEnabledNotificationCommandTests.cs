using System.Net;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Mail.Mailer.OrganizationUserAutoConfirmation;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AutoConfirmUser;
using Bit.Core.Platform.Mail.Mailer;
using Bit.Core.Test.AutoFixture.OrganizationFixtures;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;
using GlobalSettings = Bit.Core.Settings.GlobalSettings;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationUsers.AutoConfirmUser;

[SutProviderCustomize]
public class OrganizationAutoConfirmEnabledNotificationCommandTests
{
    [Theory]
    [OrganizationCustomize, BitAutoData]
    public async Task SendEmailAsync_NoEmailsProvided_ReturnsNoEmailsWereProvidedError(
        Organization organization,
        SutProvider<OrganizationAutoConfirmEnabledNotificationCommand> sutProvider)
    {
        // Arrange
        SetupGlobalSettings(sutProvider);
        var request = new OrganizationAutoConfirmEnabledNotificationRequest(organization, []);

        // Act
        var result = await sutProvider.Sut.SendEmailAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<NoEmailsWereProvided>(result.AsError);
        await sutProvider.GetDependency<IMailer>()
            .DidNotReceive()
            .SendEmail(Arg.Any<OrganizationAutoConfirmationEnabled>());
    }

    [Theory]
    [OrganizationCustomize, BitAutoData]
    public async Task SendEmailAsync_WithValidEmails_SendsEmailWithCorrectProperties(
        Organization organization,
        List<string> emails,
        SutProvider<OrganizationAutoConfirmEnabledNotificationCommand> sutProvider)
    {
        // Arrange
        const string vaultUrl = "https://vault.bitwarden.com/";
        SetupGlobalSettings(sutProvider, vaultUrl);
        var request = new OrganizationAutoConfirmEnabledNotificationRequest(organization, emails);
        var expectedUrl = $"{vaultUrl}#/organizations/{organization.Id}/settings/policies";

        // Act
        var result = await sutProvider.Sut.SendEmailAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        await sutProvider.GetDependency<IMailer>()
            .Received(1)
            .SendEmail(Arg.Is<OrganizationAutoConfirmationEnabled>(mail =>
                mail.ToEmails.SequenceEqual(emails) &&
                mail.View.WebVaultUrl == expectedUrl &&
                mail.Subject == $"Automatic user confirmation is available for {WebUtility.HtmlEncode(organization.Name)}"));
    }

    [Theory]
    [OrganizationCustomize, BitAutoData]
    public async Task SendEmailAsync_MailerThrowsException_ReturnsEmailSendingFailedError(
        Organization organization,
        List<string> emails,
        SutProvider<OrganizationAutoConfirmEnabledNotificationCommand> sutProvider)
    {
        // Arrange
        SetupGlobalSettings(sutProvider);
        sutProvider.GetDependency<IMailer>()
            .SendEmail(Arg.Any<OrganizationAutoConfirmationEnabled>())
            .ThrowsAsync(new Exception("SMTP failure"));
        var request = new OrganizationAutoConfirmEnabledNotificationRequest(organization, emails);

        // Act
        var result = await sutProvider.Sut.SendEmailAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<EmailSendingFailed>(result.AsError);
    }

    [Theory]
    [OrganizationCustomize, BitAutoData]
    public async Task SendEmailAsync_OrganizationNameWithSpecialCharacters_HtmlEncodesSubject(
        Organization organization,
        List<string> emails,
        SutProvider<OrganizationAutoConfirmEnabledNotificationCommand> sutProvider)
    {
        // Arrange
        SetupGlobalSettings(sutProvider);
        organization.Name = "Test & Company <script>";
        var request = new OrganizationAutoConfirmEnabledNotificationRequest(organization, emails);

        // Act
        await sutProvider.Sut.SendEmailAsync(request);

        // Assert
        await sutProvider.GetDependency<IMailer>()
            .Received(1)
            .SendEmail(Arg.Is<OrganizationAutoConfirmationEnabled>(mail =>
                mail.Subject == $"Automatic user confirmation is available for {WebUtility.HtmlEncode(organization.Name)}"));
    }

    private static void SetupGlobalSettings(
        SutProvider<OrganizationAutoConfirmEnabledNotificationCommand> sutProvider,
        string vaultUrl = "https://vault.bitwarden.com/")
    {
        var globalSettings = sutProvider.GetDependency<GlobalSettings>();
        globalSettings.BaseServiceUri = new GlobalSettings.BaseServiceUriSettings(globalSettings) { Vault = vaultUrl };
    }
}
