using System.Text;
using Bit.Billing.Models;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace Bit.Billing.Controllers;

[Route("paypal")]
public class PayPalController : Controller
{
    private readonly BillingSettings _billingSettings;
    private readonly ILogger<PayPalController> _logger;
    private readonly IMailService _mailService;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IPaymentService _paymentService;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IUserRepository _userRepository;
    private readonly IProviderRepository _providerRepository;

    public PayPalController(
        IOptions<BillingSettings> billingSettings,
        ILogger<PayPalController> logger,
        IMailService mailService,
        IOrganizationRepository organizationRepository,
        IPaymentService paymentService,
        ITransactionRepository transactionRepository,
        IUserRepository userRepository,
        IProviderRepository providerRepository)
    {
        _billingSettings = billingSettings?.Value;
        _logger = logger;
        _mailService = mailService;
        _organizationRepository = organizationRepository;
        _paymentService = paymentService;
        _transactionRepository = transactionRepository;
        _userRepository = userRepository;
        _providerRepository = providerRepository;
    }

    [HttpPost("ipn")]
    public async Task<IActionResult> PostIpn()
    {
        var key = HttpContext.Request.Query.ContainsKey("key")
            ? HttpContext.Request.Query["key"].ToString()
            : null;

        if (string.IsNullOrEmpty(key))
        {
            _logger.LogError("PayPal IPN: Key is missing");
            return BadRequest();
        }

        if (!CoreHelpers.FixedTimeEquals(key, _billingSettings.PayPal.WebhookKey))
        {
            _logger.LogError("PayPal IPN: Key is incorrect");
            return BadRequest();
        }

        using var streamReader = new StreamReader(HttpContext.Request.Body, Encoding.UTF8);

        var requestContent = await streamReader.ReadToEndAsync();

        if (string.IsNullOrEmpty(requestContent))
        {
            _logger.LogError("PayPal IPN: Request body is null or empty");
            return BadRequest();
        }

        var transactionModel = new PayPalIPNTransactionModel(requestContent);

        _logger.LogInformation("PayPal IPN: Transaction Type = {Type}", transactionModel.TransactionType);

        if (string.IsNullOrEmpty(transactionModel.TransactionId))
        {
            _logger.LogWarning("PayPal IPN: Transaction ID is missing");
            return Ok();
        }

        var entityId = transactionModel.UserId ?? transactionModel.OrganizationId ?? transactionModel.ProviderId;

        if (!entityId.HasValue)
        {
            _logger.LogError("PayPal IPN ({Id}): 'custom' did not contain a User ID or Organization ID or provider ID", transactionModel.TransactionId);
            return BadRequest();
        }

        if (transactionModel.TransactionType != "web_accept" &&
            transactionModel.TransactionType != "merch_pmt" &&
            transactionModel.PaymentStatus != "Refunded")
        {
            _logger.LogWarning("PayPal IPN ({Id}): Transaction type ({Type}) not supported for payments",
                transactionModel.TransactionId,
                transactionModel.TransactionType);

            return Ok();
        }

        if (transactionModel.ReceiverId != _billingSettings.PayPal.BusinessId)
        {
            _logger.LogWarning(
                "PayPal IPN ({Id}): Receiver ID ({ReceiverId}) does not match Bitwarden business ID ({BusinessId})",
                transactionModel.TransactionId,
                transactionModel.ReceiverId,
                _billingSettings.PayPal.BusinessId);

            return Ok();
        }

        if (transactionModel.PaymentStatus == "Refunded" && string.IsNullOrEmpty(transactionModel.ParentTransactionId))
        {
            _logger.LogWarning("PayPal IPN ({Id}): Parent transaction ID is required for refund", transactionModel.TransactionId);
            return Ok();
        }

        if (transactionModel.PaymentType == "echeck" && transactionModel.PaymentStatus != "Refunded")
        {
            _logger.LogWarning("PayPal IPN ({Id}): Transaction was an eCheck payment", transactionModel.TransactionId);
            return Ok();
        }

        if (transactionModel.MerchantCurrency != "USD")
        {
            _logger.LogWarning("PayPal IPN ({Id}): Transaction was not in USD ({Currency})",
                transactionModel.TransactionId,
                transactionModel.MerchantCurrency);

            return Ok();
        }

        switch (transactionModel.PaymentStatus)
        {
            case "Completed":
                {
                    var existingTransaction = await _transactionRepository.GetByGatewayIdAsync(
                        GatewayType.PayPal,
                        transactionModel.TransactionId);

                    if (existingTransaction != null)
                    {
                        _logger.LogWarning("PayPal IPN ({Id}): Already processed this completed transaction", transactionModel.TransactionId);
                        return Ok();
                    }

                    try
                    {
                        var transaction = new Transaction
                        {
                            Amount = transactionModel.MerchantGross,
                            CreationDate = transactionModel.PaymentDate,
                            OrganizationId = transactionModel.OrganizationId,
                            UserId = transactionModel.UserId,
                            ProviderId = transactionModel.ProviderId,
                            Type = transactionModel.IsAccountCredit ? TransactionType.Credit : TransactionType.Charge,
                            Gateway = GatewayType.PayPal,
                            GatewayId = transactionModel.TransactionId,
                            PaymentMethodType = PaymentMethodType.PayPal,
                            Details = transactionModel.TransactionId
                        };

                        await _transactionRepository.CreateAsync(transaction);

                        if (transactionModel.IsAccountCredit)
                        {
                            await ApplyCreditAsync(transaction);
                        }
                    }
                    // Catch foreign key violations because user/org could have been deleted.
                    catch (SqlException sqlException) when (sqlException.Number == 547)
                    {
                        _logger.LogError("PayPal IPN ({Id}): SQL Exception | {Message}", transactionModel.TransactionId, sqlException.Message);
                    }

                    break;
                }
            case "Refunded" or "Reversed":
                {
                    var existingTransaction = await _transactionRepository.GetByGatewayIdAsync(
                        GatewayType.PayPal,
                        transactionModel.TransactionId);

                    if (existingTransaction != null)
                    {
                        _logger.LogWarning("PayPal IPN ({Id}): Already processed this refunded transaction", transactionModel.TransactionId);
                        return Ok();
                    }

                    var parentTransaction = await _transactionRepository.GetByGatewayIdAsync(
                        GatewayType.PayPal,
                        transactionModel.ParentTransactionId);

                    if (parentTransaction == null)
                    {
                        _logger.LogWarning("PayPal IPN ({Id}): Could not find parent transaction", transactionModel.TransactionId);
                        return Ok();
                    }

                    var refundAmount = Math.Abs(transactionModel.MerchantGross);

                    var remainingAmount = parentTransaction.Amount - parentTransaction.RefundedAmount.GetValueOrDefault();

                    if (refundAmount > 0 && !parentTransaction.Refunded.GetValueOrDefault() && remainingAmount >= refundAmount)
                    {
                        parentTransaction.RefundedAmount = parentTransaction.RefundedAmount.GetValueOrDefault() + refundAmount;

                        if (parentTransaction.RefundedAmount == parentTransaction.Amount)
                        {
                            parentTransaction.Refunded = true;
                        }

                        await _transactionRepository.ReplaceAsync(parentTransaction);

                        await _transactionRepository.CreateAsync(new Transaction
                        {
                            Amount = refundAmount,
                            CreationDate = transactionModel.PaymentDate,
                            OrganizationId = transactionModel.OrganizationId,
                            UserId = transactionModel.UserId,
                            ProviderId = transactionModel.ProviderId,
                            Type = TransactionType.Refund,
                            Gateway = GatewayType.PayPal,
                            GatewayId = transactionModel.TransactionId,
                            PaymentMethodType = PaymentMethodType.PayPal,
                            Details = transactionModel.TransactionId
                        });
                    }

                    break;
                }
        }

        return Ok();
    }

    private async Task ApplyCreditAsync(Transaction transaction)
    {
        string billingEmail = null;

        if (transaction.OrganizationId.HasValue)
        {
            var organization = await _organizationRepository.GetByIdAsync(transaction.OrganizationId.Value);

            if (await _paymentService.CreditAccountAsync(organization, transaction.Amount))
            {
                await _organizationRepository.ReplaceAsync(organization);

                billingEmail = organization.BillingEmailAddress();
            }
        }
        else if (transaction.UserId.HasValue)
        {
            var user = await _userRepository.GetByIdAsync(transaction.UserId.Value);

            if (await _paymentService.CreditAccountAsync(user, transaction.Amount))
            {
                await _userRepository.ReplaceAsync(user);

                billingEmail = user.BillingEmailAddress();
            }
        }
        else if (transaction.ProviderId.HasValue)
        {
            var provider = await _providerRepository.GetByIdAsync(transaction.ProviderId.Value);

            if (await _paymentService.CreditAccountAsync(provider, transaction.Amount))
            {
                await _providerRepository.ReplaceAsync(provider);

                billingEmail = provider.BillingEmailAddress();
            }
        }

        if (!string.IsNullOrEmpty(billingEmail))
        {
            await _mailService.SendAddedCreditAsync(billingEmail, transaction.Amount);
        }
    }
}
