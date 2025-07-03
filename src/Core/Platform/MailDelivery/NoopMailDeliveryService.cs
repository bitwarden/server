using Bit.Core.Models.Mail;

namespace Bit.Core.Platform.MailDelivery;

internal class NoopMailDeliveryService : IMailDeliveryService
{
    public Task SendEmailAsync(MailMessage message)
    {
        return Task.FromResult(0);
    }
}
