using Bit.Core.Models.Mail;

#nullable enable

namespace Bit.Core.Services;

public class NoopMailDeliveryService : IMailDeliveryService
{
    public Task SendEmailAsync(MailMessage message)
    {
        return Task.FromResult(0);
    }
}
