using System.Text;
using Bit.Billing.Models;
using Bit.Billing.Services;
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
    private readonly IPayPalIPNClient _payPalIPNClient;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IUserRepository _userRepository;

    public PayPalController(
        IOptions<BillingSettings> billingSettings,
        ILogger<PayPalController> logger,
        IMailService mailService,
        IOrganizationRepository organizationRepository,
        IPaymentService paymentService,
        IPayPalIPNClient payPalIPNClient,
        ITransactionRepository transactionRepository,
        IUserRepository userRepository)
    {
        _billingSettings = billingSettings?.Value;
        _logger = logger;
        _mailService = mailService;
        _organizationRepository = organizationRepository;
        _paymentService = paymentService;
        _payPalIPNClient = payPalIPNClient;
        _transactionRepository = transactionRepository;
        _userRepository = userRepository;
    }

    [HttpPost("ipn")]
    public async Task<IActionResult> PostIpn()
    {
        var key = HttpContext.Request.Query.ContainsKey("key")
            ? HttpContext.Request.Query["key"].ToString()
            : null;

        if (!CoreHelpers.FixedTimeEquals(key, _billingSettings.PayPal.WebhookKey))
        {
            _logger.LogError("PayPal IPN: Webhook key is incorrect or does not exist");
            return new BadRequestResult();
        }

        using var streamReader = new StreamReader(HttpContext.Request.Body, Encoding.UTF8);

        var requestContent = await streamReader.ReadToEndAsync();

        if (string.IsNullOrWhiteSpace(requestContent))
        {
            _logger.LogError("PayPal IPN: Webhook request content was null or empty");
            return new BadRequestResult();
        }

        var transactionModel = new PayPalIPNTransactionModel(requestContent);

        var entityId = transactionModel.UserId ?? transactionModel.OrganizationId;

        if (!entityId.HasValue)
        {
            _logger.LogWarning("PayPal IPN ({Id}): Webhook did not contain User ID or Organization ID", transactionModel.TransactionId);
            return new OkResult();
        }

        var verified = await _payPalIPNClient.VerifyIPN(entityId.Value, requestContent);

        if (!verified)
        {
            _logger.LogError("PayPal IPN ({Id}): Could not verify request content", transactionModel.TransactionId);
            return new BadRequestResult();
        }

        if (transactionModel.TransactionType != "web_accept" &&
            transactionModel.TransactionType != "merch_pmt" &&
            transactionModel.PaymentStatus != "Refunded")
        {
            _logger.LogWarning("PayPal IPN ({Id}): Transaction type ({Type}) not supported for payments",
                transactionModel.TransactionId,
                transactionModel.TransactionType);

            return new OkResult();
        }

        if (transactionModel.ReceiverId != _billingSettings.PayPal.BusinessId)
        {
            _logger.LogError(
                "PayPal IPN ({Id}): Receiver ID ({ReceiverId}) does not match Bitwarden business ID ({BusinessId})",
                transactionModel.TransactionId,
                transactionModel.ReceiverId,
                _billingSettings.PayPal.BusinessId);

            return new BadRequestResult();
        }

        if (transactionModel.PaymentStatus == "Refunded" && string.IsNullOrEmpty(transactionModel.ParentTransactionId))
        {
            _logger.LogWarning("PayPal IPN ({Id}): Parent transaction ID is required for refund", transactionModel.TransactionId);
            return new OkResult();
        }

        if (transactionModel.PaymentType == "echeck" && transactionModel.PaymentStatus != "Refunded")
        {
            _logger.LogWarning("PayPal IPN ({Id}): Transaction was an eCheck payment", transactionModel.TransactionId);
            return new OkResult();
        }

        if (transactionModel.MerchantCurrency != "USD")
        {
            _logger.LogWarning("PayPal IPN ({Id}): Transaction was not in USD ({Currency})",
                transactionModel.TransactionId,
                transactionModel.MerchantCurrency);

            return new OkResult();
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
                        return new OkResult();
                    }

                    try
                    {
                        var transaction = new Transaction
                        {
                            Amount = transactionModel.MerchantGross,
                            CreationDate = transactionModel.PaymentDate,
                            OrganizationId = transactionModel.OrganizationId,
                            UserId = transactionModel.UserId,
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
                        return new OkResult();
                    }

                    var parentTransaction = await _transactionRepository.GetByGatewayIdAsync(
                        GatewayType.PayPal,
                        transactionModel.ParentTransactionId);

                    if (parentTransaction == null)
                    {
                        _logger.LogError("PayPal IPN ({Id}): Could not find parent transaction", transactionModel.TransactionId);
                        return new BadRequestResult();
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

        return new OkResult();
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

        if (!string.IsNullOrEmpty(billingEmail))
        {
            await _mailService.SendAddedCreditAsync(billingEmail, transaction.Amount);
        }
    }
}
