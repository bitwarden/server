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
}
