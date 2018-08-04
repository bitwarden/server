using Bit.Core;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Bit.Billing.Controllers
{
    [Route("jobs")]
    public class JobsController : Controller
    {
        private readonly BillingSettings _billingSettings;
        private readonly GlobalSettings _globalSettings;
        private readonly IUserRepository _userRepository;
        private readonly IMailService _mailService;

        public JobsController(
            IOptions<BillingSettings> billingSettings,
            GlobalSettings globalSettings,
            IUserRepository userRepository,
            IMailService mailService)
        {
            _billingSettings = billingSettings?.Value;
            _globalSettings = globalSettings;
            _userRepository = userRepository;
            _mailService = mailService;
        }

        [HttpPost("premium-renewal-reminders")]
        public async Task<IActionResult> PostPremiumRenewalReminders([FromQuery] string key)
        {
            if(key != _billingSettings.JobsKey)
            {
                return new BadRequestResult();
            }

            var users = await _userRepository.GetManyByPremiumRenewalAsync();
            foreach(var user in users)
            {
                var paymentService = user.GetPaymentService(_globalSettings);
                var upcomingInvoice = await paymentService.GetUpcomingInvoiceAsync(user);
                if(upcomingInvoice?.Date != null)
                {
                    var items = new List<string> { "1 × Premium Membership (Annually)" };
                    await _mailService.SendInvoiceUpcomingAsync(user.Email, upcomingInvoice.Amount,
                        upcomingInvoice.Date.Value, items, false);
                }
                await _userRepository.UpdateRenewalReminderDateAsync(user.Id, DateTime.UtcNow);
            }

            return new OkResult();
        }
    }
}
