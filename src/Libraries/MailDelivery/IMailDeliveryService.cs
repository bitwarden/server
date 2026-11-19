using Bit.Core.Models.Mail;

namespace Bit.Core.Platform.Mail.Delivery;

/// <summary>
/// Delivers fully rendered mail to an upstream provider.
/// </summary>
public interface IMailDeliveryService
{
    /// <summary>
    /// Delivers <paramref name="message"/> to the upstream provider.
    /// </summary>
    Task SendEmailAsync(MailMessage message);
}
