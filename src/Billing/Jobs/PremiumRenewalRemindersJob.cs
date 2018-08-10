using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core;
using Bit.Core.Jobs;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace Bit.Billing.Jobs
{
    public class PremiumRenewalRemindersJob : BaseJob
    {
        private readonly BillingSettings _billingSettings;
        private readonly GlobalSettings _globalSettings;
        private readonly IUserRepository _userRepository;
        private readonly IMailService _mailService;

        public PremiumRenewalRemindersJob(
            IOptions<BillingSettings> billingSettings,
            GlobalSettings globalSettings,
            IUserRepository userRepository,
            IMailService mailService,
            ILogger<PremiumRenewalRemindersJob> logger)
            : base(logger)
        {
            _billingSettings = billingSettings?.Value;
            _globalSettings = globalSettings;
            _userRepository = userRepository;
            _mailService = mailService;
        }

        protected async override Task ExecuteJobAsync(IJobExecutionContext context)
        {
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
        }
    }
}
