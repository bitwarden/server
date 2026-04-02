using Bit.Core.Models.Mail;

namespace Bit.Core.Platform.Mail.Mailer;

#nullable enable

/// <summary>
/// Generic mailer interface for sending email messages.
/// </summary>
public interface IMailer
{
    /// <summary>
    /// Sends an email message.
    /// </summary>
    /// <param name="message"></param>
    public Task SendEmail<T>(BaseMail<T> message) where T : BaseMailView;

    /// <summary>
    /// Enqueues email messages for asynchronous delivery.
    /// </summary>
    /// <param name="messages">The email messages to enqueue</param>
    /// <typeparam name="T">The type of the mail view</typeparam>
    public Task EnqueueEmailsAsync<T>(IEnumerable<BaseMail<T>> messages) where T : BaseMailView;

    /// <summary>
    /// Sends a previously enqueued IMailer message by rendering the stored view and sending.
    /// </summary>
    /// <param name="queueMessage">The enqueued message containing view data</param>
    public Task SendEnqueuedMailerMessageAsync(IMailQueueMessage queueMessage);
}
