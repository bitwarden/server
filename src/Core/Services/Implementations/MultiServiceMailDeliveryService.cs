using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Bit.Core.Settings;
using Microsoft.AspNetCore.Hosting;
using System.Net.Http;
using Bit.Core.Models.Mail;
using Bit.Core.Utilities;

namespace Bit.Core.Services
{
    public class MultiServiceMailDeliveryService : IMailDeliveryService
    {
        private readonly IMailDeliveryService _sesService;
        private readonly IMailDeliveryService _postalService;
        private readonly int _postalPercentage;

        private static Random _random = new Random();

        public MultiServiceMailDeliveryService(
            GlobalSettings globalSettings,
            IWebHostEnvironment hostingEnvironment,
            IHttpClientFactory httpClientFactory,
            ILogger<AmazonSesMailDeliveryService> sesLogger,
            ILogger<PostalMailDeliveryService> postalLogger)
        {
            _sesService = new AmazonSesMailDeliveryService(globalSettings, hostingEnvironment, sesLogger);

            if (CoreHelpers.SettingHasValue(globalSettings.Mail?.PostalApiKey) &&
                CoreHelpers.SettingHasValue(globalSettings.Mail?.PostalDomain))
            {
                _postalService = new PostalMailDeliveryService(globalSettings, postalLogger, hostingEnvironment,
                    httpClientFactory);
            }

            // 2% by default
            _postalPercentage = (globalSettings.Mail?.PostalPercentage).GetValueOrDefault(2);
        }

        public async Task SendEmailAsync(MailMessage message)
        {
            var roll = _random.Next(0, 99);
            if (_postalService != null && roll < _postalPercentage)
            {
                await _postalService.SendEmailAsync(message);
            }
            else
            {
                await _sesService.SendEmailAsync(message);
            }
        }
    }
}
