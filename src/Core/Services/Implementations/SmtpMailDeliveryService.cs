using System;
using System.Threading.Tasks;
using Bit.Core.Models.Mail;

namespace Bit.Core.Services
{
    public class SmtpMailDeliveryService : IMailDeliveryService
    {
        private readonly GlobalSettings _globalSettings;

        public SmtpMailDeliveryService(GlobalSettings globalSettings)
        {

        }

        public Task SendEmailAsync(MailMessage message)
        {
            throw new NotImplementedException();
        }
    }
}
