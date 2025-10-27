using Bit.Core.Models.Mail;
using Bit.Core.Services;

namespace Bit.Core.Platform.Mailer;

#nullable enable

public class Mailer(IMailRenderer renderer, IMailDeliveryService mailDeliveryService) : IMailer
{
    public async Task SendEmail<T>(BaseMail<T> message) where T : BaseMailView
    {
        var content = await renderer.RenderAsync(message.View);

        var metadata = new Dictionary<string, object>();
        if (message.IgnoreSuppressList)
        {
            metadata.Add("SendGridBypassListManagement", true);
        }

        var mailMessage = new MailMessage
        {
            ToEmails = message.ToEmails,
            Subject = message.Subject,
            MetaData = metadata,
            HtmlContent = content.html,
            TextContent = content.txt,
            Category = message.Category,
        };

        await mailDeliveryService.SendEmailAsync(mailMessage);
    }
}
