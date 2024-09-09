using Bit.Core.Models.Mail;

#nullable enable

namespace Bit.Core.Services;

public interface IMailDeliveryService
{
    Task SendEmailAsync(MailMessage message);
}
