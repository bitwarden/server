using Bit.Core.Models.Mail;
using Bit.Core.Platform.Mailer;
using Bit.Core.Services;
using Bit.Core.Test.Platform.Services.TestMail;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Platform.Services;

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
}
