using Bit.Billing.Utilities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Data.SqlClient;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Bit.Billing.Controllers
{
    [Route("paypal")]
    public class PayPalController : Controller
    {
        private readonly BillingSettings _billingSettings;
        private readonly PayPalClient _paypalClient;
        private readonly ITransactionRepository _transactionRepository;

        public PayPalController(
            IOptions<BillingSettings> billingSettings,
            PayPalClient paypalClient,
            ITransactionRepository transactionRepository)
        {
            _billingSettings = billingSettings?.Value;
            _paypalClient = paypalClient;
            _transactionRepository = transactionRepository;
        }

        [HttpPost("webhook")]
        public async Task<IActionResult> PostWebhook([FromQuery] string key)
        {
            if(key != _billingSettings.PayPal.WebhookKey)
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

            if(body == null)
            {
                return new BadRequestResult();
            }

            var verified = await _paypalClient.VerifyWebhookAsync(body, HttpContext.Request.Headers,
                _billingSettings.PayPal.WebhookId);
            if(!verified)
            {
                return new BadRequestResult();
            }

            if(body.Contains("\"PAYMENT.SALE.COMPLETED\""))
            {
                var ev = JsonConvert.DeserializeObject<PayPalClient.Event<PayPalClient.Sale>>(body);
                var sale = ev.Resource;
                var saleTransaction = await _transactionRepository.GetByGatewayIdAsync(
                    GatewayType.PayPal, sale.Id);
                if(saleTransaction == null)
                {
                    var ids = sale.GetIdsFromCustom();
                    if(ids.Item1.HasValue || ids.Item2.HasValue)
                    {
                        try
                        {
                            await _transactionRepository.CreateAsync(new Core.Models.Table.Transaction
                            {
                                Amount = sale.Amount.TotalAmount,
                                CreationDate = sale.CreateTime,
                                OrganizationId = ids.Item1,
                                UserId = ids.Item2,
                                Type = TransactionType.Charge,
                                Gateway = GatewayType.PayPal,
                                GatewayId = sale.Id,
                                PaymentMethodType = PaymentMethodType.PayPal,
                                Details = sale.Id
                            });
                        }
                        // Catch foreign key violations because user/org could have been deleted.
                        catch(SqlException e) when(e.Number == 547) { }
                    }
                }
            }
            else if(body.Contains("\"PAYMENT.SALE.REFUNDED\""))
            {
                var ev = JsonConvert.DeserializeObject<PayPalClient.Event<PayPalClient.Refund>>(body);
                var refund = ev.Resource;
                var refundTransaction = await _transactionRepository.GetByGatewayIdAsync(
                    GatewayType.PayPal, refund.Id);
                if(refundTransaction == null)
                {
                    var saleTransaction = await _transactionRepository.GetByGatewayIdAsync(
                        GatewayType.PayPal, refund.SaleId);
                    if(saleTransaction == null)
                    {
                        return new BadRequestResult();
                    }

                    if(!saleTransaction.Refunded.GetValueOrDefault() &&
                        saleTransaction.RefundedAmount.GetValueOrDefault() < refund.TotalRefundedAmount.ValueAmount)
                    {
                        saleTransaction.RefundedAmount = refund.TotalRefundedAmount.ValueAmount;
                        if(saleTransaction.RefundedAmount == saleTransaction.Amount)
                        {
                            saleTransaction.Refunded = true;
                        }
                        await _transactionRepository.ReplaceAsync(saleTransaction);

                        var ids = refund.GetIdsFromCustom();
                        if(ids.Item1.HasValue || ids.Item2.HasValue)
                        {
                            await _transactionRepository.CreateAsync(new Core.Models.Table.Transaction
                            {
                                Amount = refund.Amount.TotalAmount,
                                CreationDate = refund.CreateTime,
                                OrganizationId = ids.Item1,
                                UserId = ids.Item2,
                                Type = TransactionType.Refund,
                                Gateway = GatewayType.PayPal,
                                GatewayId = refund.Id,
                                PaymentMethodType = PaymentMethodType.PayPal,
                                Details = refund.Id
                            });
                        }
                    }
                }
            }

            return new OkResult();
        }
    }
}
