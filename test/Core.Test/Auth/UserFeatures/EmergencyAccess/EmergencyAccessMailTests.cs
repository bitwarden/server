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
    // Constant values for all Emergency Access emails
    private const string _emergencyAccessHelpUrl = "https://bitwarden.com/help/emergency-access/";
    private const string _emergencyAccessMailSubject = "Emergency contacts removed";

    /// <summary>
    /// Documents how to construct and send the emergency access removal email.
    /// 1. Inject IMailer into their command/service
    /// 2. Construct EmergencyAccessRemoveGranteesMail as shown below
    /// 3. Call mailer.SendEmail(mail)
    /// </summary>
    [Theory, BitAutoData]
    public async Task SendEmergencyAccessRemoveGranteesEmail_SingleGrantee_Success(
        string grantorEmail,
        string granteeName)
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
                RemovedGranteeNames = [granteeName]
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

        // Verify the content contains the grantee name
        Assert.Contains(granteeName, sentMessage.TextContent);
        Assert.Contains(granteeName, sentMessage.HtmlContent);
    }

    /// <summary>
    /// Documents handling multiple removed grantees in a single email.
    /// </summary>
    [Theory, BitAutoData]
    public async Task SendEmergencyAccessRemoveGranteesEmail_MultipleGrantees_RendersAllNames(
        string grantorEmail)
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
                RemovedGranteeNames = granteeNames
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
    /// Validates the required GranteeNames for the email view model.
    /// </summary>
    [Theory, BitAutoData]
    public void EmergencyAccessRemoveGranteesMailView_GranteeNames_AreRequired(
        string grantorEmail)
    {
        // Arrange - Shows the minimum required to construct the email
        var mail = new EmergencyAccessRemoveGranteesMail
        {
            ToEmails = [grantorEmail],  // Required: who to send to
            View = new EmergencyAccessRemoveGranteesMailView
            {
                // Required: at least one removed grantee name
                RemovedGranteeNames = ["Example Grantee"]
            }
        };

        // Assert
        Assert.NotNull(mail);
        Assert.NotNull(mail.View);
        Assert.NotEmpty(mail.View.RemovedGranteeNames);
    }

    [Theory, BitAutoData]
    public void EmergencyAccessRemoveGranteesMailView_SubjectAndHelpLink_MatchesExpectedValues(string grantorEmail, string granteeName)
    {
        // Arrange
        var mail = new EmergencyAccessRemoveGranteesMail
        {
            ToEmails = [grantorEmail],
            View = new EmergencyAccessRemoveGranteesMailView { RemovedGranteeNames = [granteeName] }
        };

        // Assert
        Assert.NotNull(mail);
        Assert.NotNull(mail.View);
        Assert.Equal(_emergencyAccessMailSubject, mail.Subject);
        Assert.Equal(_emergencyAccessHelpUrl, mail.View.EmergencyAccessHelpPageUrl);
    }
}
