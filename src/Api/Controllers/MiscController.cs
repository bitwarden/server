using System;
using Microsoft.AspNetCore.Mvc;
using Bit.Core.Models.Api;
using System.Threading.Tasks;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Api.Controllers
{
    public class MiscController : Controller
    {
        private readonly BitPayClient _bitPayClient;

        public MiscController(BitPayClient bitPayClient)
        {
            _bitPayClient = bitPayClient;
        }

        [HttpGet("~/alive")]
        [HttpGet("~/now")]
        public DateTime Get()
        {
            return DateTime.UtcNow;
        }

        [HttpGet("~/version")]
        public VersionResponseModel Version()
        {
            return new VersionResponseModel();
        }

        [HttpGet("~/ip")]
        public JsonResult Ip()
        {
            return new JsonResult(new
            {
                Ip = HttpContext.Connection?.RemoteIpAddress?.ToString(),
                Headers = HttpContext.Request?.Headers,
            });
        }

        [Authorize("Application")]
        [HttpPost("~/bitpay-invoice")]
        [SelfHosted(NotSelfHostedOnly = true)]
        public async Task<string> PostBitPayInvoice([FromBody]BitPayInvoiceRequestModel model)
        {
            var invoice = await _bitPayClient.CreateInvoiceAsync(model.ToBitpayClientInvoice());
            return invoice.Url;
        }
    }
}
