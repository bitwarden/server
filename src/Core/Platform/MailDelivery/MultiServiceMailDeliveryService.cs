using Bit.Core.Models.Mail;
using Bit.Core.Settings;

namespace Bit.Core.Platform.MailDelivery;

internal class MultiServiceMailDeliveryService : IMailDeliveryService
{
    private readonly IMailDeliveryService _primaryService;
    private readonly IMailDeliveryService _secondaryService;
    private readonly int _sendGridPercentage;

    private static readonly Random _random = new Random();

    public MultiServiceMailDeliveryService(
        GlobalSettings globalSettings,
        IMailDeliveryService primaryService,
        IMailDeliveryService secondaryService)
    {
        _primaryService = primaryService;
        _secondaryService = secondaryService;

        // disabled by default (-1)
        _sendGridPercentage = (globalSettings.Mail?.SendGridPercentage).GetValueOrDefault(-1);
    }

    public async Task SendEmailAsync(MailMessage message)
    {
        var roll = _random.Next(0, 99);
        if (roll < _sendGridPercentage)
        {
            await _secondaryService.SendEmailAsync(message);
        }
        else
        {
            await _primaryService.SendEmailAsync(message);
        }
    }
}
