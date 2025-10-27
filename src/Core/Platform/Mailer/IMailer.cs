namespace Bit.Core.Platform.Mailer;

#nullable enable

/// <summary>
///
/// </summary>
public interface IMailer
{
    /// <summary>
    /// Sends an email message.
    /// </summary>
    /// <param name="message"></param>
    public Task SendEmail<T>(BaseMail<T> message) where T : BaseMailView;
}
