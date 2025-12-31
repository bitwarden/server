using Bit.Core.Auth.UserFeatures.EmergencyAccess.Mail;
using Bit.Core.Models.Mail;
using Bit.Core.Platform.Mail.Delivery;
using Bit.Core.Platform.Mail.Mailer;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using GlobalSettings = Bit.Core.Settings.GlobalSettings;

namespace Bit.Core.Test.Auth.UserFeatures.EmergencyAccess;

[SutProviderCustomize]
public class EmergencyAccessMailTests
{
    /// <summary>
    /// Documents how to construct and send the emergency access removal email.
    /// 1. Inject IMailer into their command/service
    /// 2. Get WebVaultUrl from GlobalSettings.BaseServiceUri.VaultWithHash
    /// 3. Construct EmergencyAccessRemoveGranteesMail as shown below
    /// 4. Call mailer.SendEmail(mail)
    /// </summary>
    [Theory, BitAutoData]
    public async Task SendEmergencyAccessRemoveGranteesEmail_SingleGrantee_Success(
        string grantorEmail,
        string granteeName,
        string webVaultUrl)
    {
        // Arrange
        var logger = Substitute.For<ILogger<HandlebarMailRenderer>>();
        var globalSettings = new GlobalSettings { SelfHosted = false };
        var deliveryService = Substitute.For<IMailDeliveryService>();
        var mailer = new Mailer(
            new HandlebarMailRenderer(logger, globalSettings),
            deliveryService);

        var mail = new EmergencyAccessRemoveGranteesMail
        {
            ToEmails = [grantorEmail],
            View = new EmergencyAccessRemoveGranteesMailView
            {
                RemovedGranteeNames = [granteeName],
                WebVaultUrl = webVaultUrl
            }
        };

        MailMessage sentMessage = null;
        await deliveryService.SendEmailAsync(Arg.Do<MailMessage>(message =>
            sentMessage = message
        ));

        // Act
        await mailer.SendEmail(mail);

        // Assert
        Assert.NotNull(sentMessage);
        Assert.Contains(grantorEmail, sentMessage.ToEmails);
        Assert.Equal("Emergency contacts removed", sentMessage.Subject);

        // Verify the content contains the grantee name
        Assert.Contains(granteeName, sentMessage.TextContent);
        Assert.Contains(granteeName, sentMessage.HtmlContent);

        // Verify the vault link is present
        Assert.Contains(webVaultUrl, sentMessage.HtmlContent);
        Assert.Contains("web app", sentMessage.HtmlContent);
    }

    /// <summary>
    /// Documents handling multiple removed grantees in a single email.
    /// </summary>
    [Theory, BitAutoData]
    public async Task SendEmergencyAccessRemoveGranteesEmail_MultipleGrantees_RendersAllNames(
        string grantorEmail,
        string webVaultUrl)
    {
        // Arrange
        var logger = Substitute.For<ILogger<HandlebarMailRenderer>>();
        var globalSettings = new GlobalSettings { SelfHosted = false };
        var deliveryService = Substitute.For<IMailDeliveryService>();
        var mailer = new Mailer(
            new HandlebarMailRenderer(logger, globalSettings),
            deliveryService);

        var granteeNames = new[] { "Alice", "Bob", "Carol" };

        var mail = new EmergencyAccessRemoveGranteesMail
        {
            ToEmails = [grantorEmail],
            View = new EmergencyAccessRemoveGranteesMailView
            {
                RemovedGranteeNames = granteeNames,
                WebVaultUrl = webVaultUrl
            }
        };

        MailMessage sentMessage = null;
        await deliveryService.SendEmailAsync(Arg.Do<MailMessage>(message =>
            sentMessage = message
        ));

        // Act
        await mailer.SendEmail(mail);

        // Assert - All grantee names should appear in the email
        Assert.NotNull(sentMessage);
        foreach (var granteeName in granteeNames)
        {
            Assert.Contains(granteeName, sentMessage.TextContent);
            Assert.Contains(granteeName, sentMessage.HtmlContent);
        }
    }

    /// <summary>
    /// Validates the minimal required fields for the email view model.
    /// Both RemovedGranteeNames and WebVaultUrl are marked as 'required' in the view model.
    /// </summary>
    [Theory, BitAutoData]
    public void EmergencyAccessRemoveGranteesMailView_RequiredFields_MustBeProvided(
        string grantorEmail,
        string webVaultUrl)
    {
        // Arrange - Shows the minimum required to construct the email
        var mail = new EmergencyAccessRemoveGranteesMail
        {
            ToEmails = [grantorEmail],  // Required: who to send to
            View = new EmergencyAccessRemoveGranteesMailView
            {
                // Required: at least one removed grantee name
                RemovedGranteeNames = ["Example Grantee"],
                // Required: link to vault for managing emergency contacts
                // In production: use GlobalSettings.BaseServiceUri.VaultWithHash
                WebVaultUrl = webVaultUrl
            }
        };

        // Assert - If this compiles and constructs, required fields are satisfied
        Assert.NotNull(mail);
        Assert.NotNull(mail.View);
        Assert.NotEmpty(mail.View.RemovedGranteeNames);
        Assert.NotNull(mail.View.WebVaultUrl);
        Assert.Equal("Emergency contacts removed", mail.Subject);
    }
}
