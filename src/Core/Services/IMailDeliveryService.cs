using Bit.Core.Models.Mail;

namespace Bit.Core.Services;

public interface IMailDeliveryService
{
    Task SendEmailAsync(MailMessage message);
}
