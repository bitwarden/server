namespace Bit.Core.Platform.Services;

#nullable enable

/// <summary>
///
/// </summary>
public interface IMailer
{
    /// <summary>
    /// Sends an email message to the specified recipient.
    /// </summary>
    /// <param name="message"></param>
    /// <param name="recipient">Recipient email</param>
    public void SendEmail(BaseMailModel2 message, string recipient);

    /// <summary>
    /// Sends multiple emails message to the specified recipients.
    /// </summary>
    /// <param name="message"></param>
    /// <param name="recipients">Recipient emails</param>
    public void SendEmails(BaseMailModel2 message, string[] recipients);
}
