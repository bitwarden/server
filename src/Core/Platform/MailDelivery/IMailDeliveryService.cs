using Bit.Core.Models.Mail;

namespace Bit.Core.Platform.MailDelivery;

public interface IMailDeliveryService
{
    Task SendEmailAsync(MailMessage message);
}
