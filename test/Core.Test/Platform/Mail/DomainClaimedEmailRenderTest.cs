using Bit.Core.AdminConsole.Entities;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Platform.Mail.Delivery;
using Bit.Core.Platform.Mail.Enqueuing;
using Bit.Core.Services.Mail;
using Bit.Core.Settings;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Platform.Mail;

public class DomainClaimedEmailRenderTest
{
    [Fact]
    public async Task RenderDomainClaimedEmail_ToVerifyTemplate()
    {
        // Arrange
        var globalSettings = new GlobalSettings
        {
            Mail = new GlobalSettings.MailSettings
            {
                ReplyToEmail = "no-reply@bitwarden.com",
                Smtp = new GlobalSettings.MailSettings.SmtpSettings
                {
                    Host = "localhost",
                    Port = 1025,
                    StartTls = false,
                    Ssl = false
                }
            },
            SiteName = "Bitwarden"
        };

        var mailDeliveryService = Substitute.For<IMailDeliveryService>();
        var mailEnqueuingService = new BlockingMailEnqueuingService();
        var distributedCache = Substitute.For<IDistributedCache>();
        var logger = Substitute.For<ILogger<HandlebarsMailService>>();

        var mailService = new HandlebarsMailService(
            globalSettings,
            mailDeliveryService,
            mailEnqueuingService,
            distributedCache,
            logger
        );

        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            Name = "Acme Corporation"
        };

        var testEmails = new List<string>
        {
            "alice@acme.com",
            "bob@acme.com",
            "charlie@acme.com"
        };

        var emailList = new ClaimedUserDomainClaimedEmails(
            testEmails,
            organization,
            "acme.com"
        );

        // Act
        await mailService.SendClaimedDomainUserEmailAsync(emailList);

        // Assert - Verify emails were sent
        await mailDeliveryService.Received(3).SendEmailAsync(Arg.Any<Bit.Core.Models.Mail.MailMessage>());

        // Capture all the emails that were sent and verify content
        var calls = mailDeliveryService.ReceivedCalls()
            .Where(call => call.GetMethodInfo().Name == "SendEmailAsync")
            .ToList();

        Assert.Equal(3, calls.Count);

        foreach (var call in calls)
        {
            var mailMessage = call.GetArguments()[0] as Bit.Core.Models.Mail.MailMessage;
            Assert.NotNull(mailMessage);

            var recipient = mailMessage.ToEmails.First();

            // Verify key content is present in each email
            Assert.Contains("@acme.com", mailMessage.HtmlContent);
            Assert.Contains(recipient, mailMessage.HtmlContent);
            Assert.DoesNotContain("[at]", mailMessage.HtmlContent);
            Assert.DoesNotContain("[dot]", mailMessage.HtmlContent);
        }
    }

    [Fact]
    public async Task SendDomainClaimedEmail_ToMailCatcher()
    {
        // Arrange
        var globalSettings = new GlobalSettings
        {
            Mail = new GlobalSettings.MailSettings
            {
                ReplyToEmail = "no-reply@bitwarden.com",
                Smtp = new GlobalSettings.MailSettings.SmtpSettings
                {
                    Host = "localhost",
                    Port = 10250, // MailCatcher SMTP port from docker-compose.yml
                    StartTls = false,
                    Ssl = false
                }
            },
            SiteName = "Bitwarden"
        };

        var mailDeliveryLogger = Substitute.For<ILogger<MailKitSmtpMailDeliveryService>>();
        var mailDeliveryService = new MailKitSmtpMailDeliveryService(globalSettings, mailDeliveryLogger);
        var mailEnqueuingService = new BlockingMailEnqueuingService();
        var distributedCache = Substitute.For<IDistributedCache>();
        var logger = Substitute.For<ILogger<HandlebarsMailService>>();

        var mailService = new HandlebarsMailService(
            globalSettings,
            mailDeliveryService,
            mailEnqueuingService,
            distributedCache,
            logger
        );

        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            Name = "Acme Corporation"
        };

        var testEmails = new List<string>
        {
            "alice@acme.com",
            "bob@acme.com"
        };

        var emailList = new ClaimedUserDomainClaimedEmails(
            testEmails,
            organization,
            "acme.com"
        );

        // Act
        await mailService.SendClaimedDomainUserEmailAsync(emailList);

        // Assert
        // Manual verification: View emails at http://localhost:1080
        // Verify the emails contain:
        //   - @acme.com (with proper @ and . characters)
        //   - Full email addresses (alice@acme.com, bob@acme.com)
        //   - NO [at] or [dot] replacements
        //   - Roboto font styling
        //   - Buildings icon at 155x155
        //   - Bitwarden logo in blue header
    }

    [Fact(Skip = "This test sends actual emails and is for manual template verification only")]
    public async Task RenderDomainClaimedEmail_WithSpecialCharacters()
    {
        // Arrange
        var globalSettings = new GlobalSettings
        {
            Mail = new GlobalSettings.MailSettings
            {
                Smtp = new GlobalSettings.MailSettings.SmtpSettings
                {
                    Host = "localhost",
                    Port = 1025,
                    StartTls = false,
                    Ssl = false
                }
            },
            SiteName = "Bitwarden"
        };

        var mailDeliveryService = Substitute.For<IMailDeliveryService>();
        var mailEnqueuingService = new BlockingMailEnqueuingService();
        var distributedCache = Substitute.For<IDistributedCache>();
        var logger = Substitute.For<ILogger<HandlebarsMailService>>();

        var mailService = new HandlebarsMailService(
            globalSettings,
            mailDeliveryService,
            mailEnqueuingService,
            distributedCache,
            logger
        );

        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            Name = "Test Corp & Co."
        };

        var testEmails = new List<string>
        {
            "test.user+tag@example.com"
        };

        var emailList = new ClaimedUserDomainClaimedEmails(
            testEmails,
            organization,
            "example.com"
        );

        // Act
        await mailService.SendClaimedDomainUserEmailAsync(emailList);

        // Assert
        // Manual verification: Check MailCatcher to verify @ and . are displayed correctly
    }
}
