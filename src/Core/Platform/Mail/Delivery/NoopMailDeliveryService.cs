using Bit.Core.Models.Mail;

namespace Bit.Core.Services.Mail.Delivery;

public class NoopMailDeliveryService : IMailDeliveryService
{
    public Task SendEmailAsync(MailMessage message)
    {
        return Task.FromResult(0);
    }
}
