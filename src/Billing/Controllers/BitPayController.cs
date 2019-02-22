using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Bit.Billing.Controllers
{
    [Route("bitpay")]
    public class BitPayController : Controller
    {
        private readonly BillingSettings _billingSettings;
        private readonly BitPayClient _bitPayClient;
        private readonly ITransactionRepository _transactionRepository;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IUserRepository _userRepository;
        private readonly IMailService _mailService;
        private readonly IPaymentService _paymentService;

        public BitPayController(
            IOptions<BillingSettings> billingSettings,
            BitPayClient bitPayClient,
            ITransactionRepository transactionRepository,
            IOrganizationRepository organizationRepository,
            IUserRepository userRepository,
            IMailService mailService,
            IPaymentService paymentService)
        {
            _billingSettings = billingSettings?.Value;
            _bitPayClient = bitPayClient;
            _transactionRepository = transactionRepository;
            _organizationRepository = organizationRepository;
            _userRepository = userRepository;
            _mailService = mailService;
            _paymentService = paymentService;
        }

        [HttpPost("ipn")]
        public async Task<IActionResult> PostIpn([FromQuery] string key)
        {
            if(key != _billingSettings.BitPayWebhookKey)
            {
                return new BadRequestResult();
            }

            if(HttpContext?.Request == null)
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

            return new OkResult();
        }
    }
}
