using Bit.Core.Models.Mail;
using Bit.Core.Platform.Mail.Delivery;
using Bit.Core.Platform.Mail.Enqueuing;

namespace Bit.Core.Platform.Mail.Mailer;

public class Mailer(
    IMailRenderer renderer,
    IMailDeliveryService mailDeliveryService,
    IMailEnqueuingService mailEnqueuingService) : IMailer
{
    public async Task SendEmail<T>(BaseMail<T> message) where T : BaseMailView
    {
        var content = await renderer.RenderAsync(message.View);

        var mailMessage = new MailMessage
        {
            ToEmails = message.ToEmails,
            Subject = message.Subject,
            MetaData = BuildMailMetadata(message.IgnoreSuppressList),
            HtmlContent = content.html,
            TextContent = content.txt,
            Category = message.Category,
        };

        await mailDeliveryService.SendEmailAsync(mailMessage);
    }

    public async Task EnqueueEmailsAsync<T>(IEnumerable<BaseMail<T>> messages) where T : BaseMailView
    {
        var queueMessages = messages.Select(message => new MailQueueMessage
        {
            Subject = message.Subject,
            ToEmails = message.ToEmails,
            Category = message.Category ?? "Default",
            TemplateName = typeof(T).AssemblyQualifiedName ?? throw new InvalidOperationException(),
            Model = message.View,
            IsMailerMessage = true,
            MetaData = BuildMailMetadata(message.IgnoreSuppressList)
        })
            .ToList();

        await mailEnqueuingService.EnqueueManyAsync(queueMessages, SendEnqueuedMailerMessageAsync);
    }

    /// <summary>
    /// Sends a previously enqueued IMailer message by rendering the stored view and sending.
    /// </summary>
    public async Task SendEnqueuedMailerMessageAsync(IMailQueueMessage mailQueueMessage)
    {
        if (!mailQueueMessage.IsMailerMessage)
        {
            throw new InvalidOperationException(
                "Expected IMailer message (IsMailerMessage = true)");
        }

        var viewType = Type.GetType(mailQueueMessage.TemplateName);
        if (viewType == null)
        {
            throw new InvalidOperationException(
                $"Could not resolve view type: {mailQueueMessage.TemplateName}");
        }

        if (mailQueueMessage.Model is not BaseMailView view)
        {
            throw new InvalidOperationException(
                $"Model is not a BaseMailView: {mailQueueMessage.Model.GetType().FullName}");
        }

        var content = await renderer.RenderAsync(view);

        var mailMessage = new MailMessage
        {
            ToEmails = mailQueueMessage.ToEmails,
            Subject = mailQueueMessage.Subject,
            BccEmails = mailQueueMessage.BccEmails,
            Category = mailQueueMessage.Category,
            MetaData = mailQueueMessage.MetaData ?? new Dictionary<string, object>(),
            HtmlContent = content.html,
            TextContent = content.txt
        };

        await mailDeliveryService.SendEmailAsync(mailMessage);
    }

    /// <summary>
    /// Builds metadata dictionary for mail delivery based on mail settings.
    /// </summary>
    private static Dictionary<string, object> BuildMailMetadata(bool ignoreSuppressList) =>
        ignoreSuppressList
            ? new Dictionary<string, object> { { "SendGridBypassListManagement", true } }
            : new Dictionary<string, object>();
}
