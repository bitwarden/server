using Bit.Core.Models.Mail;
using Bit.Core.Platform.Services;
using Bit.Core.Services;
using Bit.Core.Test.Platform.TestMail;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Platform;

public class MailerTest
{
    [Fact]
    public async Task SendEmailAsync()
    {
        var deliveryService = Substitute.For<IMailDeliveryService>();
        var mailer = new Mailer(new HandlebarMailRenderer(), deliveryService);

        var mail = new TestMail.TestMail()
        {
            ToEmails = ["test@bw.com"],
            View = new TestMailView() { Token = "", Email = "", WebVaultUrl = "" }
        };

        MailMessage? sentMessage = null;
        await deliveryService.SendEmailAsync(Arg.Do<MailMessage>(message =>
            sentMessage = message
        ));

        await mailer.SendEmail(mail);

        Assert.NotNull(sentMessage);
        Assert.Contains("test@bw.com", sentMessage.ToEmails);
        Assert.Equal("Test Email", sentMessage.Subject);
        Assert.Equivalent("Test Email\n\n/redirect-connector.html#finish-signup?token=&email=&fromEmail=true\n",
            sentMessage.TextContent);
        Assert.Equivalent("Test <b>Email</b>\n\n<a href=\"/redirect-connector.html#finish-signup?token=&email=&fromEmail=true\">Test</a>\n",
            sentMessage.HtmlContent);
    }
}
