using Bit.Billing.Utilities;
using Bit.Core;
using Bit.Core.Enums;
using Bit.Core.Models.Table;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Bit.Billing.Controllers
{
    [Route("apple")]
    public class AppleController : Controller
    {
        private readonly BillingSettings _billingSettings;
        private readonly ITransactionRepository _transactionRepository;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IUserRepository _userRepository;
        private readonly IMailService _mailService;
        private readonly IPaymentService _paymentService;
        private readonly ILogger<AppleController> _logger;

        public AppleController(
            IOptions<BillingSettings> billingSettings,
            ITransactionRepository transactionRepository,
            IOrganizationRepository organizationRepository,
            IUserRepository userRepository,
            IMailService mailService,
            IPaymentService paymentService,
            ILogger<AppleController> logger)
        {
            _billingSettings = billingSettings?.Value;
            _transactionRepository = transactionRepository;
            _organizationRepository = organizationRepository;
            _userRepository = userRepository;
            _mailService = mailService;
            _paymentService = paymentService;
            _logger = logger;
        }

        [HttpPost("iap")]
        public async Task<IActionResult> PostIap()
        {
            if(HttpContext?.Request?.Query == null)
            {
                return new BadRequestResult();
            }

            var key = HttpContext.Request.Query.ContainsKey("key") ?
                HttpContext.Request.Query["key"].ToString() : null;
            if(key != _billingSettings.AppleWebhookKey)
            {
                return new BadRequestResult();
            }

            string body = null;
            using(var reader = new StreamReader(HttpContext.Request.Body, Encoding.UTF8))
            {
                body = await reader.ReadToEndAsync();
            }

            if(string.IsNullOrWhiteSpace(body))
            {
                return new BadRequestResult();
            }

            _logger.LogInformation(Constants.BypassFiltersEventId, "Got IAP Status Update");
            _logger.LogInformation(Constants.BypassFiltersEventId, body);

            return new OkResult();
        }
    }
}
