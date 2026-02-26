using Bit.Core.Models.Mail;
using Bit.Core.Platform.Mail.Delivery;
using Bit.Core.Platform.Mail.Enqueuing;

namespace Bit.Core.Platform.Mail.Mailer;

#nullable enable

public class Mailer(
    IMailRenderer renderer,
    IMailDeliveryService mailDeliveryService,
    IMailEnqueuingService mailEnqueuingService) : IMailer
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

    public async Task EnqueueEmail<T>(BaseMail<T> message) where T : BaseMailView
    {
        var content = await renderer.RenderAsync(message.View);

        var queueMessage = new MailQueueMessage
        {
            Subject = message.Subject,
            ToEmails = message.ToEmails,
            Category = message.Category,
            HtmlContent = content.html,
            TextContent = content.txt,
            IgnoreSuppressList = message.IgnoreSuppressList,
        };

        await mailEnqueuingService.EnqueueAsync(queueMessage, SendPreRenderedFallbackAsync);
    }

    private async Task SendPreRenderedFallbackAsync(IMailQueueMessage queueMessage)
    {
        var metadata = new Dictionary<string, object>();
        if (queueMessage.IgnoreSuppressList)
        {
            metadata.Add("SendGridBypassListManagement", true);
        }

        var mailMessage = new MailMessage
        {
            Subject = queueMessage.Subject,
            ToEmails = queueMessage.ToEmails,
            BccEmails = queueMessage.BccEmails,
            Category = queueMessage.Category,
            HtmlContent = queueMessage.HtmlContent,
            TextContent = queueMessage.TextContent,
            MetaData = metadata,
        };
        await mailDeliveryService.SendEmailAsync(mailMessage);
    }
}
