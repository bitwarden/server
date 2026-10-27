using Bit.Core.Models.Mail;

namespace Bit.Core.Platform.Mail.Delivery;

/// <summary>
/// An <see cref="IMailDeliveryService"/> that drops every message without sending it.
/// </summary>
public class NoopMailDeliveryService : IMailDeliveryService
{
    /// <summary>
    /// Drops <paramref name="message"/> without sending it.
    /// </summary>
    public Task SendEmailAsync(MailMessage message)
    {
        return Task.FromResult(0);
    }
}
