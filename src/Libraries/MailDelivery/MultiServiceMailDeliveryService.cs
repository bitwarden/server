using Bit.Core.Models.Mail;
using Microsoft.Extensions.Options;

namespace Bit.Core.Platform.Mail.Delivery;

internal class MultiServiceMailDeliveryService : IMailDeliveryService
{
    private readonly IMailDeliveryService _sesService;
    private readonly IMailDeliveryService _sendGridService;
    private readonly int _sendGridPercentage;

    private static Random _random = new Random();

    public MultiServiceMailDeliveryService(
        IOptions<MailDeliveryOptions> options,
        AmazonSesMailDeliveryService sesService,
        SendGridMailDeliveryService sendGridService)
    {
        _sesService = sesService;
        _sendGridService = sendGridService;

        // disabled by default (-1)
        _sendGridPercentage = options.Value.SendGridPercentage.GetValueOrDefault(-1);
    }

    public async Task SendEmailAsync(MailMessage message)
    {
        var roll = _random.Next(0, 99);
        if (roll < _sendGridPercentage)
        {
            await _sendGridService.SendEmailAsync(message);
        }
        else
        {
            await _sesService.SendEmailAsync(message);
        }
    }
}
