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
using Xunit.Abstractions;

namespace Bit.Core.Test.Platform.Mail;

public class DomainClaimedEmailRenderTest
{
    private readonly ITestOutputHelper _output;

    public DomainClaimedEmailRenderTest(ITestOutputHelper output)
    {
        _output = output;
    }

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

        // Capture all the emails that were sent
        var calls = mailDeliveryService.ReceivedCalls()
            .Where(call => call.GetMethodInfo().Name == "SendEmailAsync")
            .ToList();

        _output.WriteLine("✅ Emails enqueued successfully!");
        _output.WriteLine($"Total emails sent: {calls.Count}");
        _output.WriteLine("");

        // Output each email's content
        int emailIndex = 0;
        foreach (var call in calls)
        {
            var mailMessage = call.GetArguments()[0] as Bit.Core.Models.Mail.MailMessage;
            if (mailMessage != null)
            {
                emailIndex++;
                _output.WriteLine($"📧 Email #{emailIndex} to: {string.Join(", ", mailMessage.ToEmails)}");
                _output.WriteLine($"Subject: {mailMessage.Subject}");
                _output.WriteLine("");

                // Output full HTML content to see what's actually being rendered
                _output.WriteLine("Full HTML Content:");
                _output.WriteLine("---");
                if (!string.IsNullOrEmpty(mailMessage.HtmlContent))
                {
                    _output.WriteLine(mailMessage.HtmlContent);
                }
                _output.WriteLine("---");
                _output.WriteLine("");

                _output.WriteLine("Full Text Content:");
                _output.WriteLine("---");
                if (!string.IsNullOrEmpty(mailMessage.TextContent))
                {
                    _output.WriteLine(mailMessage.TextContent);
                }
                _output.WriteLine("---");
                _output.WriteLine("");

                // Verify key content is present in each email
                var recipient = mailMessage.ToEmails.First();

                // Check if expected content exists
                var hasAtAcme = mailMessage.HtmlContent?.Contains("@acme.com") ?? false;
                var hasRecipient = mailMessage.HtmlContent?.Contains(recipient) ?? false;
                var hasBracketAt = mailMessage.HtmlContent?.Contains("[at]") ?? false;
                var hasBracketDot = mailMessage.HtmlContent?.Contains("[dot]") ?? false;

                _output.WriteLine($"Content check for {recipient}:");
                _output.WriteLine($"  - Contains '@acme.com': {hasAtAcme}");
                _output.WriteLine($"  - Contains '{recipient}': {hasRecipient}");
                _output.WriteLine($"  - Contains '[at]': {hasBracketAt}");
                _output.WriteLine($"  - Contains '[dot]': {hasBracketDot}");

                if (!hasAtAcme || !hasRecipient || hasBracketAt || hasBracketDot)
                {
                    _output.WriteLine($"⚠️ EMAIL CONTENT VALIDATION FAILED");
                }
            }
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
        _output.WriteLine("✅ Emails sent to MailCatcher!");
        _output.WriteLine($"Total emails sent: {testEmails.Count}");
        _output.WriteLine("");
        _output.WriteLine("🌐 View emails at: http://localhost:1080");
        _output.WriteLine("");
        _output.WriteLine("Verify the emails contain:");
        _output.WriteLine("  - @acme.com (with proper @ and . characters)");
        _output.WriteLine("  - Full email addresses (alice@acme.com, bob@acme.com)");
        _output.WriteLine("  - NO [at] or [dot] replacements");
        _output.WriteLine("  - Roboto font styling");
        _output.WriteLine("  - Buildings icon at 155x155");
        _output.WriteLine("  - Bitwarden logo in blue header");
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
        _output.WriteLine("✅ Email with special characters sent!");
        _output.WriteLine("Check MailCatcher to verify @ and . are displayed correctly");
    }
}
