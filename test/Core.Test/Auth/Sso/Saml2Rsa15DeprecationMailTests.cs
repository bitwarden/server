using System.Reflection;
using Bit.Core.Auth.Sso.Mail;
using Bit.Core.Models.Mail;
using Bit.Core.Platform.Mail.Delivery;
using Bit.Core.Platform.Mail.Mailer;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using GlobalSettings = Bit.Core.Settings.GlobalSettings;

namespace Bit.Core.Test.Auth.Sso;

public class Saml2Rsa15DeprecationMailTests
{
    private const string _expectedSubject = "Security alert: Update your identity provider's single sign-on encryption method";

    private static async Task<(MailMessage? Sent, Saml2Rsa15DeprecationMail Mail)> SendAsync(string[] toEmails)
    {
        var logger = Substitute.For<ILogger<HandlebarMailRenderer>>();
        var globalSettings = new GlobalSettings { SelfHosted = false };
        var deliveryService = Substitute.For<IMailDeliveryService>();
        var mailer = new Mailer(
            new HandlebarMailRenderer(logger, globalSettings),
            deliveryService);

        var mail = new Saml2Rsa15DeprecationMail
        {
            ToEmails = toEmails,
            View = new Saml2Rsa15DeprecationMailView()
        };

        MailMessage? sentMessage = null;
        await deliveryService.SendEmailAsync(Arg.Do<MailMessage>(message =>
            sentMessage = message
        ));

        await mailer.SendEmail(mail);

        return (sentMessage, mail);
    }

    [Fact]
    public async Task SendEmail_RendersHtmlAndTextWithDateAndHelpLink()
    {
        var (sentMessage, _) = await SendAsync(["admin@example.com"]);

        Assert.NotNull(sentMessage);
        Assert.False(string.IsNullOrWhiteSpace(sentMessage.HtmlContent));
        Assert.False(string.IsNullOrWhiteSpace(sentMessage.TextContent));

        foreach (var content in new[] { sentMessage.HtmlContent, sentMessage.TextContent })
        {
            Assert.Contains("December 1, 2026", content);
            Assert.Contains("RSAES-PKCS1 v1.5", content);
            Assert.Contains("https://bitwarden.com/help/RSAES-PKCS1/", content);
            Assert.DoesNotContain("{{", content);
        }

        Assert.Contains("Update your IdP's SSO encryption method", sentMessage.HtmlContent);
    }

    [Fact]
    public async Task SendEmail_SetsSubjectAndRecipientsOnTheSentMessage()
    {
        string[] recipients = ["admin1@example.com", "admin2@example.com"];

        var (sentMessage, _) = await SendAsync(recipients);

        Assert.NotNull(sentMessage);
        Assert.Equal(_expectedSubject, sentMessage.Subject);
        Assert.Equal(recipients, sentMessage.ToEmails);
    }

    [Fact]
    public void View_ContainsNoOrganizationOrUserData()
    {
        var propertyNames = typeof(Saml2Rsa15DeprecationMailView)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        string[] expected = ["CurrentYear", "DeprecationDate", "HelpPageUrl"];

        Assert.Equal(expected, propertyNames);
    }
}
