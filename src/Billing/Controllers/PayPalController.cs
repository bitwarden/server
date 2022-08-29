using System.Data.SqlClient;
using System.Text;
using Bit.Billing.Utilities;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Bit.Billing.Controllers;

[Route("paypal")]
public class PayPalController : Controller
{
    private readonly BillingSettings _billingSettings;
    private readonly PayPalIpnClient _paypalIpnClient;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IUserRepository _userRepository;
    private readonly IMailService _mailService;
    private readonly IPaymentService _paymentService;
    private readonly ILogger<PayPalController> _logger;

    public PayPalController(
        IOptions<BillingSettings> billingSettings,
        PayPalIpnClient paypalIpnClient,
        ITransactionRepository transactionRepository,
        IOrganizationRepository organizationRepository,
        IUserRepository userRepository,
        IMailService mailService,
        IPaymentService paymentService,
        ILogger<PayPalController> logger)
    {
        _billingSettings = billingSettings?.Value;
        _paypalIpnClient = paypalIpnClient;
        _transactionRepository = transactionRepository;
        _organizationRepository = organizationRepository;
        _userRepository = userRepository;
        _mailService = mailService;
        _paymentService = paymentService;
        _logger = logger;
    }

    [HttpPost("ipn")]
    public async Task<IActionResult> PostIpn()
    {
        _logger.LogDebug("PayPal webhook has been hit.");
        if (HttpContext?.Request?.Query == null)
        {
            return new BadRequestResult();
        }

        var key = HttpContext.Request.Query.ContainsKey("key") ?
            HttpContext.Request.Query["key"].ToString() : null;
        if (!CoreHelpers.FixedTimeEquals(key, _billingSettings.PayPal.WebhookKey))
        {
            _logger.LogWarning("PayPal webhook key is incorrect or does not exist.");
            return new BadRequestResult();
        }

        string body = null;
        using (var reader = new StreamReader(HttpContext.Request.Body, Encoding.UTF8))
        {
            body = await reader.ReadToEndAsync();
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            return new BadRequestResult();
        }

        var verified = await _paypalIpnClient.VerifyIpnAsync(body);
        if (!verified)
        {
            _logger.LogWarning("Unverified IPN received.");
            return new BadRequestResult();
        }

        var ipnTransaction = new PayPalIpnClient.IpnTransaction(body);
        if (ipnTransaction.TxnType != "web_accept" && ipnTransaction.TxnType != "merch_pmt" &&
            ipnTransaction.PaymentStatus != "Refunded")
        {
            // Only processing billing agreement payments, buy now button payments, and refunds for now.
            return new OkResult();
        }

        if (ipnTransaction.ReceiverId != _billingSettings.PayPal.BusinessId)
        {
            _logger.LogWarning("Receiver was not proper business id. " + ipnTransaction.ReceiverId);
            return new BadRequestResult();
        }

        if (ipnTransaction.PaymentStatus == "Refunded" && ipnTransaction.ParentTxnId == null)
        {
            // Refunds require parent transaction
            return new OkResult();
        }

        if (ipnTransaction.PaymentType == "echeck" && ipnTransaction.PaymentStatus != "Refunded")
        {
            // Not accepting eChecks, unless it is a refund
            _logger.LogWarning("Got an eCheck payment. " + ipnTransaction.TxnId);
            return new OkResult();
        }

        if (ipnTransaction.McCurrency != "USD")
        {
            // Only process USD payments
            _logger.LogWarning("Received a payment not in USD. " + ipnTransaction.TxnId);
            return new OkResult();
        }

        var ids = ipnTransaction.GetIdsFromCustom();
        if (!ids.Item1.HasValue && !ids.Item2.HasValue)
        {
            return new OkResult();
        }

        if (ipnTransaction.PaymentStatus == "Completed")
        {
            var transaction = await _transactionRepository.GetByGatewayIdAsync(
                GatewayType.PayPal, ipnTransaction.TxnId);
            if (transaction != null)
            {
                _logger.LogWarning("Already processed this completed transaction. #" + ipnTransaction.TxnId);
                return new OkResult();
            }

            var isAccountCredit = ipnTransaction.IsAccountCredit();
            try
            {
                var tx = new Transaction
                {
                    Amount = ipnTransaction.McGross,
                    CreationDate = ipnTransaction.PaymentDate,
                    OrganizationId = ids.Item1,
                    UserId = ids.Item2,
                    Type = isAccountCredit ? TransactionType.Credit : TransactionType.Charge,
                    Gateway = GatewayType.PayPal,
                    GatewayId = ipnTransaction.TxnId,
                    PaymentMethodType = PaymentMethodType.PayPal,
                    Details = ipnTransaction.TxnId
                };
                await _transactionRepository.CreateAsync(tx);

                if (isAccountCredit)
                {
                    string billingEmail = null;
                    if (tx.OrganizationId.HasValue)
                    {
                        var org = await _organizationRepository.GetByIdAsync(tx.OrganizationId.Value);
                        if (org != null)
                        {
                            billingEmail = org.BillingEmailAddress();
                            if (await _paymentService.CreditAccountAsync(org, tx.Amount))
                            {
                                await _organizationRepository.ReplaceAsync(org);
                            }
                        }
                    }
                    else
                    {
                        var user = await _userRepository.GetByIdAsync(tx.UserId.Value);
                        if (user != null)
                        {
                            billingEmail = user.BillingEmailAddress();
                            if (await _paymentService.CreditAccountAsync(user, tx.Amount))
                            {
                                await _userRepository.ReplaceAsync(user);
                            }
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(billingEmail))
                    {
                        await _mailService.SendAddedCreditAsync(billingEmail, tx.Amount);
                    }
                }
            }
            // Catch foreign key violations because user/org could have been deleted.
            catch (SqlException e) when (e.Number == 547) { }
        }
        else if (ipnTransaction.PaymentStatus == "Refunded" || ipnTransaction.PaymentStatus == "Reversed")
        {
            var refundTransaction = await _transactionRepository.GetByGatewayIdAsync(
                GatewayType.PayPal, ipnTransaction.TxnId);
            if (refundTransaction != null)
            {
                _logger.LogWarning("Already processed this refunded transaction. #" + ipnTransaction.TxnId);
                return new OkResult();
            }

            var parentTransaction = await _transactionRepository.GetByGatewayIdAsync(
                GatewayType.PayPal, ipnTransaction.ParentTxnId);
            if (parentTransaction == null)
            {
                _logger.LogWarning("Parent transaction was not found. " + ipnTransaction.TxnId);
                return new BadRequestResult();
            }

            var refundAmount = System.Math.Abs(ipnTransaction.McGross);
            var remainingAmount = parentTransaction.Amount -
                parentTransaction.RefundedAmount.GetValueOrDefault();
            if (refundAmount > 0 && !parentTransaction.Refunded.GetValueOrDefault() &&
                remainingAmount >= refundAmount)
            {
                parentTransaction.RefundedAmount =
                    parentTransaction.RefundedAmount.GetValueOrDefault() + refundAmount;
                if (parentTransaction.RefundedAmount == parentTransaction.Amount)
                {
                    parentTransaction.Refunded = true;
                }

                await _transactionRepository.ReplaceAsync(parentTransaction);
                await _transactionRepository.CreateAsync(new Transaction
                {
                    Amount = refundAmount,
                    CreationDate = ipnTransaction.PaymentDate,
                    OrganizationId = ids.Item1,
                    UserId = ids.Item2,
                    Type = TransactionType.Refund,
                    Gateway = GatewayType.PayPal,
                    GatewayId = ipnTransaction.TxnId,
                    PaymentMethodType = PaymentMethodType.PayPal,
                    Details = ipnTransaction.TxnId
                });
            }
        }

        return new OkResult();
    }
}
