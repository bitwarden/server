using Bit.Core.Models.Mail;
using Bit.Core.Models.Mail.Billing.Renewal.BusinessPlanRenewal2020Migration;
using Bit.Core.Platform.Mail.Delivery;
using Bit.Core.Platform.Mail.Mailer;
using Bit.Core.Settings;
using Bit.Core.Test.Platform.Mailer.TestMail;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Platform.Mailer;

public class MailerTest
{
    [Fact]
    public async Task SendEmailAsync()
    {
        var logger = Substitute.For<ILogger<HandlebarMailRenderer>>();
        var globalSettings = new GlobalSettings { SelfHosted = false };
        var deliveryService = Substitute.For<IMailDeliveryService>();

        var mailer = new Core.Platform.Mail.Mailer.Mailer(new HandlebarMailRenderer(logger, globalSettings), deliveryService);

        var mail = new TestMail.TestMail()
        {
            ToEmails = ["test@bw.com"],
            View = new TestMailView() { Name = "John Smith" }
        };

        MailMessage? sentMessage = null;
        await deliveryService.SendEmailAsync(Arg.Do<MailMessage>(message =>
            sentMessage = message
        ));

        await mailer.SendEmail(mail);

        Assert.NotNull(sentMessage);
        Assert.Contains("test@bw.com", sentMessage.ToEmails);
        Assert.Equal("Test Email", sentMessage.Subject);
        Assert.Equivalent("Hello John Smith", sentMessage.TextContent.Trim());
        Assert.Equivalent("Hello <b>John Smith</b>", sentMessage.HtmlContent.Trim());
    }

    [Fact]
    public async Task SendBusinessPlanRenewal2020MigrationEmail_WithDiscount_RendersDiscountLine()
    {
        var mailer = BuildMailer(out var deliveryService);

        var mail = new BusinessPlanRenewal2020MigrationMail
        {
            ToEmails = ["org@example.com"],
            View = new BusinessPlanRenewal2020MigrationMailView
            {
                RenewalDate = "June 12, 2026",
                Seats = 320,
                PerUserMonthlyPrice = "$7.00",
                AnnualTotalPrice = "$18,432.00",
                DiscountPercent = "25%"
            }
        };

        MailMessage? sentMessage = null;
        await deliveryService.SendEmailAsync(Arg.Do<MailMessage>(message => sentMessage = message));

        await mailer.SendEmail(mail);

        Assert.NotNull(sentMessage);
        Assert.Equal("Your Bitwarden subscription price is changing", sentMessage.Subject);
        // Both templates resolve as embedded resources and the {{#if HasDiscount}} block renders.
        foreach (var body in new[] { sentMessage.HtmlContent, sentMessage.TextContent })
        {
            Assert.Contains("June 12, 2026", body);
            Assert.Contains("320", body);
            Assert.Contains("$7.00", body);
            Assert.Contains("25% discount", body);
            Assert.Contains("$18,432.00", body);
        }
    }

    [Fact]
    public async Task SendBusinessPlanRenewal2020MigrationEmail_WithoutDiscount_OmitsDiscountLine()
    {
        var mailer = BuildMailer(out var deliveryService);

        var mail = new BusinessPlanRenewal2020MigrationMail
        {
            ToEmails = ["org@example.com"],
            View = new BusinessPlanRenewal2020MigrationMailView
            {
                RenewalDate = "June 12, 2026",
                Seats = 320,
                PerUserMonthlyPrice = "$7.00",
                AnnualTotalPrice = "$23,040.00",
                DiscountPercent = null
            }
        };

        MailMessage? sentMessage = null;
        await deliveryService.SendEmailAsync(Arg.Do<MailMessage>(message => sentMessage = message));

        await mailer.SendEmail(mail);

        Assert.NotNull(sentMessage);
        // Both bodies render (proving the templates resolve)...
        Assert.False(string.IsNullOrWhiteSpace(sentMessage.HtmlContent));
        Assert.False(string.IsNullOrWhiteSpace(sentMessage.TextContent));
        Assert.Contains("$23,040.00", sentMessage.HtmlContent);
        // ...and the {{#if HasDiscount}} discount line is skipped.
        Assert.DoesNotContain("discount", sentMessage.HtmlContent);
        Assert.DoesNotContain("discount", sentMessage.TextContent);
    }

    private static Core.Platform.Mail.Mailer.Mailer BuildMailer(out IMailDeliveryService deliveryService)
    {
        var logger = Substitute.For<ILogger<HandlebarMailRenderer>>();
        var globalSettings = new GlobalSettings { SelfHosted = false };
        deliveryService = Substitute.For<IMailDeliveryService>();
        return new Core.Platform.Mail.Mailer.Mailer(new HandlebarMailRenderer(logger, globalSettings), deliveryService);
    }
}
