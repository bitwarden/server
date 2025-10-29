using Bit.Core.Models.Mail;
using Bit.Core.Settings;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Services;

public class MultiServiceMailDeliveryService : IMailDeliveryService
{
    private readonly IMailDeliveryService _sesService;
    private readonly IMailDeliveryService _sendGridService;
    private readonly int _sendGridPercentage;

    private static Random _random = new Random();

    public MultiServiceMailDeliveryService(
        GlobalSettings globalSettings,
        IWebHostEnvironment hostingEnvironment,
        ILogger<AmazonSesMailDeliveryService> sesLogger,
        ILogger<SendGridMailDeliveryService> sendGridLogger)
    {
        _sesService = new AmazonSesMailDeliveryService(globalSettings, hostingEnvironment, sesLogger);
        _sendGridService = new SendGridMailDeliveryService(globalSettings, hostingEnvironment, sendGridLogger);

        // disabled by default (-1)
        _sendGridPercentage = (globalSettings.Mail?.SendGridPercentage).GetValueOrDefault(-1);
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
