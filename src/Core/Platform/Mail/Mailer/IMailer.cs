namespace Bit.Core.Platform.Mail.Mailer;

#nullable enable

/// <summary>
/// Generic mailer interface for sending email messages.
/// </summary>
public interface IMailer
{
    /// <summary>
    /// Sends an email message synchronously via the mail delivery service.
    /// </summary>
    public Task SendEmail<T>(BaseMail<T> message) where T : BaseMailView;

    /// <summary>
    /// Pre-renders an email and enqueues it for background delivery.
    /// In cloud environments this writes to the Azure Storage Queue and returns immediately.
    /// In self-hosted environments this falls back to synchronous delivery.
    /// </summary>
    public Task EnqueueEmail<T>(BaseMail<T> message) where T : BaseMailView;
}
