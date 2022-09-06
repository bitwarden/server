using System.Data.SqlClient;
using System.Globalization;
using Bit.Billing.Models;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Bit.Billing.Controllers;

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
    private readonly ILogger<BitPayController> _logger;

    public BitPayController(
        IOptions<BillingSettings> billingSettings,
        BitPayClient bitPayClient,
        ITransactionRepository transactionRepository,
        IOrganizationRepository organizationRepository,
        IUserRepository userRepository,
        IMailService mailService,
        IPaymentService paymentService,
        ILogger<BitPayController> logger)
    {
        _billingSettings = billingSettings?.Value;
        _bitPayClient = bitPayClient;
        _transactionRepository = transactionRepository;
        _organizationRepository = organizationRepository;
        _userRepository = userRepository;
        _mailService = mailService;
        _paymentService = paymentService;
        _logger = logger;
    }

    [HttpPost("ipn")]
    public async Task<IActionResult> PostIpn([FromBody] BitPayEventModel model, [FromQuery] string key)
    {
        if (!CoreHelpers.FixedTimeEquals(key, _billingSettings.BitPayWebhookKey))
        {
            return new BadRequestResult();
        }
        if (model == null || string.IsNullOrWhiteSpace(model.Data?.Id) ||
            string.IsNullOrWhiteSpace(model.Event?.Name))
        {
            return new BadRequestResult();
        }

        if (model.Event.Name != "invoice_confirmed")
        {
            // Only processing confirmed invoice events for now.
            return new OkResult();
        }

        var invoice = await _bitPayClient.GetInvoiceAsync(model.Data.Id);
        if (invoice == null)
        {
            // Request forged...?
            _logger.LogWarning("Invoice not found. #" + model.Data.Id);
            return new BadRequestResult();
        }

        if (invoice.Status != "confirmed" && invoice.Status != "completed")
        {
            _logger.LogWarning("Invoice status of '" + invoice.Status + "' is not acceptable. #" + invoice.Id);
            return new BadRequestResult();
        }

        if (invoice.Currency != "USD")
        {
            // Only process USD payments
            _logger.LogWarning("Non USD payment received. #" + invoice.Id);
            return new OkResult();
        }

        var ids = GetIdsFromPosData(invoice);
        if (!ids.Item1.HasValue && !ids.Item2.HasValue)
        {
            return new OkResult();
        }

        var isAccountCredit = IsAccountCredit(invoice);
        if (!isAccountCredit)
        {
            // Only processing credits
            _logger.LogWarning("Non-credit payment received. #" + invoice.Id);
            return new OkResult();
        }

        var transaction = await _transactionRepository.GetByGatewayIdAsync(GatewayType.BitPay, invoice.Id);
        if (transaction != null)
        {
            _logger.LogWarning("Already processed this invoice. #" + invoice.Id);
            return new OkResult();
        }

        try
        {
            var tx = new Transaction
            {
                Amount = Convert.ToDecimal(invoice.Price),
                CreationDate = GetTransactionDate(invoice),
                OrganizationId = ids.Item1,
                UserId = ids.Item2,
                Type = TransactionType.Credit,
                Gateway = GatewayType.BitPay,
                GatewayId = invoice.Id,
                PaymentMethodType = PaymentMethodType.BitPay,
                Details = $"{invoice.Currency}, BitPay {invoice.Id}"
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

        return new OkResult();
    }

    private bool IsAccountCredit(BitPayLight.Models.Invoice.Invoice invoice)
    {
        return invoice != null && invoice.PosData != null && invoice.PosData.Contains("accountCredit:1");
    }

    private DateTime GetTransactionDate(BitPayLight.Models.Invoice.Invoice invoice)
    {
        var transactions = invoice.Transactions?.Where(t => t.Type == null &&
            !string.IsNullOrWhiteSpace(t.Confirmations) && t.Confirmations != "0");
        if (transactions != null && transactions.Count() == 1)
        {
            return DateTime.Parse(transactions.First().ReceivedTime, CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind);
        }
        return CoreHelpers.FromEpocMilliseconds(invoice.CurrentTime);
    }

    public Tuple<Guid?, Guid?> GetIdsFromPosData(BitPayLight.Models.Invoice.Invoice invoice)
    {
        Guid? orgId = null;
        Guid? userId = null;

        if (invoice != null && !string.IsNullOrWhiteSpace(invoice.PosData) && invoice.PosData.Contains(":"))
        {
            var mainParts = invoice.PosData.Split(',');
            foreach (var mainPart in mainParts)
            {
                var parts = mainPart.Split(':');
                if (parts.Length > 1 && Guid.TryParse(parts[1], out var id))
                {
                    if (parts[0] == "userId")
                    {
                        userId = id;
                    }
                    else if (parts[0] == "organizationId")
                    {
                        orgId = id;
                    }
                }
            }
        }

        return new Tuple<Guid?, Guid?>(orgId, userId);
    }
}
