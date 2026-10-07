using System.Globalization;
using Bit.Billing.Models;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Payment.Clients;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using BitPayLight.Models.Invoice;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace Bit.Billing.Controllers;

using static BitPayConstants;
using static StripeConstants;

[Route("bitpay")]
[ApiExplorerSettings(IgnoreApi = true)]
public class BitPayController(
    GlobalSettings globalSettings,
    IBitPayClient bitPayClient,
    ITransactionRepository transactionRepository,
    IOrganizationRepository organizationRepository,
    IUserRepository userRepository,
    IProviderRepository providerRepository,
    IMailService mailService,
    IStripePaymentService paymentService,
    ILogger<BitPayController> logger,
    IPremiumUserBillingService premiumUserBillingService)
    : Controller
{
    [HttpPost("ipn")]
    public async Task<IActionResult> PostIpn([FromBody] BitPayEventModel model, [FromQuery] string key)
    {
        if (!CoreHelpers.FixedTimeEquals(key, globalSettings.BitPay.WebhookKey))
        {
            return new BadRequestObjectResult("Invalid key");
        }

        var invoice = await bitPayClient.GetInvoice(model.Data.Id);

        if (invoice.Currency != "USD")
        {
            logger.LogWarning("Received BitPay invoice webhook for invoice ({InvoiceID}) with non-USD currency: {Currency}", invoice.Id, invoice.Currency);
            return new BadRequestObjectResult("Cannot process non-USD payments");
        }

        var (organizationId, userId, providerId) = GetIdsFromPosData(invoice);
        if ((!organizationId.HasValue && !userId.HasValue && !providerId.HasValue) || !invoice.PosData.Contains(PosDataKeys.AccountCredit))
        {
            logger.LogWarning("Received BitPay invoice webhook for invoice ({InvoiceID}) that had invalid POS data: {PosData}", invoice.Id, invoice.PosData);
            return new BadRequestObjectResult("Invalid POS data");
        }

        if (invoice.Status != InvoiceStatuses.Complete)
        {
            logger.LogInformation("Received valid BitPay invoice webhook for invoice ({InvoiceID}) that is not yet complete: {Status}",
                invoice.Id, invoice.Status);
            return new OkObjectResult("Waiting for invoice to be completed");
        }

        var existingTransaction = await transactionRepository.GetByGatewayIdAsync(GatewayType.BitPay, invoice.Id);
        if (existingTransaction != null)
        {
            logger.LogWarning("Already processed BitPay invoice webhook for invoice ({InvoiceID})", invoice.Id);
            return new OkObjectResult("Invoice already processed");
        }

        try
        {
            var transaction = new Transaction
            {
                Amount = Convert.ToDecimal(invoice.Price),
                CreationDate = GetTransactionDate(invoice),
                OrganizationId = organizationId,
                UserId = userId,
                ProviderId = providerId,
                Type = TransactionType.Credit,
                Gateway = GatewayType.BitPay,
                GatewayId = invoice.Id,
                PaymentMethodType = PaymentMethodType.BitPay,
                Details = $"{invoice.Currency}, BitPay {invoice.Id}"
            };

            await transactionRepository.CreateAsync(transaction);

            var billingEmail = "";
            if (transaction.OrganizationId.HasValue)
            {
                var organization = await organizationRepository.GetByIdAsync(transaction.OrganizationId.Value);
                if (organization != null)
                {
                    billingEmail = organization.BillingEmailAddress();
                    if (await paymentService.CreditAccountAsync(organization, transaction.Amount))
                    {
                        await organizationRepository.ReplaceAsync(organization);
                    }
                }
            }
            else if (transaction.UserId.HasValue)
            {
                var user = await userRepository.GetByIdAsync(transaction.UserId.Value);
                if (user != null)
                {
                    billingEmail = user.BillingEmailAddress();
                    await premiumUserBillingService.Credit(user, transaction.Amount);
                }
            }
            else if (transaction.ProviderId.HasValue)
            {
                var provider = await providerRepository.GetByIdAsync(transaction.ProviderId.Value);
                if (provider != null)
                {
                    billingEmail = provider.BillingEmailAddress();
                    if (await paymentService.CreditAccountAsync(provider, transaction.Amount))
                    {
                        await providerRepository.ReplaceAsync(provider);
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(billingEmail))
            {
                await mailService.SendAddedCreditAsync(billingEmail, transaction.Amount);
            }
        }
        // Catch foreign key violations because user/org could have been deleted.
        catch (SqlException e) when (e.Number == 547)
        {
        }

        return new OkResult();
    }

    private static DateTime GetTransactionDate(Invoice invoice)
    {
        var transactions = invoice.Transactions?.Where(transaction =>
            transaction.Type == null && !string.IsNullOrWhiteSpace(transaction.Confirmations) &&
            transaction.Confirmations != "0").ToList();

        return transactions?.Count == 1
            ? DateTime.Parse(transactions.First().ReceivedTime, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
            : CoreHelpers.FromEpocMilliseconds(invoice.CurrentTime);
    }

    public (Guid? OrganizationId, Guid? UserId, Guid? ProviderId) GetIdsFromPosData(Invoice invoice)
    {
        if (invoice.PosData is null or { Length: 0 } || !invoice.PosData.Contains(':'))
        {
            return new ValueTuple<Guid?, Guid?, Guid?>(null, null, null);
        }

        var ids = invoice.PosData
            .Split(',')
            .Select(part => part.Split(':'))
            .Where(parts => parts.Length == 2 && Guid.TryParse(parts[1], out _))
            .ToDictionary(parts => parts[0], parts => Guid.Parse(parts[1]));

        return new ValueTuple<Guid?, Guid?, Guid?>(
            ids.TryGetValue(MetadataKeys.OrganizationId, out var id) ? id : null,
            ids.TryGetValue(MetadataKeys.UserId, out id) ? id : null,
            ids.TryGetValue(MetadataKeys.ProviderId, out id) ? id : null
        );
    }
}
