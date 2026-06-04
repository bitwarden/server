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
                IsAnnual = true,
                TotalPrice = "$18,432.00",
                DiscountLines = ["25%"]
            }
        };

        MailMessage? sentMessage = null;
        await deliveryService.SendEmailAsync(Arg.Do<MailMessage>(message => sentMessage = message));

        await mailer.SendEmail(mail);

        Assert.NotNull(sentMessage);
        Assert.Equal("Your Bitwarden subscription price is changing", sentMessage.Subject);
        // Both templates resolve as embedded resources and the {{#each DiscountLines}} block renders.
        foreach (var body in new[] { sentMessage.HtmlContent, sentMessage.TextContent })
        {
            Assert.Contains("June 12, 2026", body);
            Assert.Contains("320", body);
            Assert.Contains("$7.00", body);
            Assert.Contains("25% discount", body);
            Assert.Contains("$18,432.00", body);
            // An annual cohort renders the per-year period label.
            Assert.Contains("total / year", body);
        }
    }

    [Fact]
    public async Task SendBusinessPlanRenewal2020MigrationEmail_WithMultipleDiscounts_RendersOneLinePerDiscount()
    {
        var mailer = BuildMailer(out var deliveryService);

        var mail = new BusinessPlanRenewal2020MigrationMail
        {
            ToEmails = ["org@example.com"],
            View = new BusinessPlanRenewal2020MigrationMailView
            {
                RenewalDate = "June 12, 2026",
                Seats = 320,
                PerUserMonthlyPrice = "$6",
                IsAnnual = true,
                // 20% + 10% applied additively to $23,040 -> $16,128.
                TotalPrice = "$16,128",
                DiscountLines = ["20%", "10%"]
            }
        };

        MailMessage? sentMessage = null;
        await deliveryService.SendEmailAsync(Arg.Do<MailMessage>(message => sentMessage = message));

        await mailer.SendEmail(mail);

        Assert.NotNull(sentMessage);
        // Each discount renders on its own line in both bodies, and the summed total is quoted.
        foreach (var body in new[] { sentMessage.HtmlContent, sentMessage.TextContent })
        {
            Assert.Contains("20% discount", body);
            Assert.Contains("10% discount", body);
            Assert.Contains("$16,128", body);
        }
    }

    [Fact]
    public async Task SendBusinessPlanRenewal2020MigrationEmail_WithMixedDiscounts_RendersPercentageAndAmountLines()
    {
        var mailer = BuildMailer(out var deliveryService);

        var mail = new BusinessPlanRenewal2020MigrationMail
        {
            ToEmails = ["org@example.com"],
            View = new BusinessPlanRenewal2020MigrationMailView
            {
                RenewalDate = "June 12, 2026",
                Seats = 320,
                PerUserMonthlyPrice = "$6",
                IsAnnual = true,
                // $23,040 -20% = $18,432, then -$50 fixed = $18,382. The handler formats the fixed amount with
                // FormatCurrency, which trims .00 from whole-dollar amounts (so "$50", not "$50.00").
                TotalPrice = "$18,382",
                DiscountLines = ["20%", "$50"]
            }
        };

        MailMessage? sentMessage = null;
        await deliveryService.SendEmailAsync(Arg.Do<MailMessage>(message => sentMessage = message));

        await mailer.SendEmail(mail);

        Assert.NotNull(sentMessage);
        // A percentage line and a fixed-amount line both render, and the combined total is quoted.
        foreach (var body in new[] { sentMessage.HtmlContent, sentMessage.TextContent })
        {
            Assert.Contains("20% discount", body);
            Assert.Contains("$50 discount", body);
            Assert.Contains("$18,382", body);
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
                IsAnnual = false,
                TotalPrice = "$2,240.00",
                DiscountLines = []
            }
        };

        MailMessage? sentMessage = null;
        await deliveryService.SendEmailAsync(Arg.Do<MailMessage>(message => sentMessage = message));

        await mailer.SendEmail(mail);

        Assert.NotNull(sentMessage);
        // Both bodies render (proving the templates resolve)...
        Assert.False(string.IsNullOrWhiteSpace(sentMessage.HtmlContent));
        Assert.False(string.IsNullOrWhiteSpace(sentMessage.TextContent));
        Assert.Contains("$2,240.00", sentMessage.HtmlContent);
        // ...a monthly cohort renders the per-month period label (not per-year)...
        foreach (var body in new[] { sentMessage.HtmlContent, sentMessage.TextContent })
        {
            Assert.Contains("total / month", body);
            Assert.DoesNotContain("total / year", body);
        }
        // ...and the {{#if HasDiscount}} discount line is skipped.
        Assert.DoesNotContain("discount", sentMessage.HtmlContent);
        Assert.DoesNotContain("discount", sentMessage.TextContent);
    }

    [Fact]
    public async Task SendBusinessPlanRenewal2020MigrationEmail_WithDiscountOnMonthlyCadence_RendersPerMonthTotal()
    {
        var mailer = BuildMailer(out var deliveryService);

        var mail = new BusinessPlanRenewal2020MigrationMail
        {
            ToEmails = ["org@example.com"],
            View = new BusinessPlanRenewal2020MigrationMailView
            {
                RenewalDate = "June 12, 2026",
                Seats = 320,
                PerUserMonthlyPrice = "$5",
                IsAnnual = false,
                // Monthly TeamsPlan total $1,600 less 20% = $1,280, quoted per month.
                TotalPrice = "$1,280",
                DiscountLines = ["20%"]
            }
        };

        MailMessage? sentMessage = null;
        await deliveryService.SendEmailAsync(Arg.Do<MailMessage>(message => sentMessage = message));

        await mailer.SendEmail(mail);

        Assert.NotNull(sentMessage);
        // The discount branch ({{#if HasDiscount}}) must also honor the monthly cadence: the total reads
        // "total / month", not "total / year", even though a discount line is present.
        foreach (var body in new[] { sentMessage.HtmlContent, sentMessage.TextContent })
        {
            Assert.Contains("20% discount", body);
            Assert.Contains("total / month", body);
            Assert.DoesNotContain("total / year", body);
        }
    }

    private static Core.Platform.Mail.Mailer.Mailer BuildMailer(out IMailDeliveryService deliveryService)
    {
        var logger = Substitute.For<ILogger<HandlebarMailRenderer>>();
        var globalSettings = new GlobalSettings { SelfHosted = false };
        deliveryService = Substitute.For<IMailDeliveryService>();
        return new Core.Platform.Mail.Mailer.Mailer(new HandlebarMailRenderer(logger, globalSettings), deliveryService);
    }
}
