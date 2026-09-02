using Bit.Core.Models.Mail;

namespace Bit.Core.Platform.Mail.Delivery;

public interface IMailDeliveryService
{
    Task SendEmailAsync(MailMessage message);
}
