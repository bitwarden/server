using Bit.Core.Models.Mail;

namespace Bit.Core.Services.Mail.Delivery;

public interface IMailDeliveryService
{
    Task SendEmailAsync(MailMessage message);
}
