using Bit.Billing.Constants;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Microsoft.Data.SqlClient;
using Event = Stripe.Event;
using Transaction = Bit.Core.Entities.Transaction;
using TransactionType = Bit.Core.Enums.TransactionType;
namespace Bit.Billing.Services.Implementations;

public class ChargeRefundedHandler : IChargeRefundedHandler
{
    private readonly ILogger<ChargeRefundedHandler> _logger;
    private readonly IStripeEventService _stripeEventService;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IStripeEventUtilityService _stripeEventUtilityService;

    public ChargeRefundedHandler(
        ILogger<ChargeRefundedHandler> logger,
        IStripeEventService stripeEventService,
        ITransactionRepository transactionRepository,
        IStripeEventUtilityService stripeEventUtilityService)
    {
        _logger = logger;
        _stripeEventService = stripeEventService;
        _transactionRepository = transactionRepository;
        _stripeEventUtilityService = stripeEventUtilityService;
    }

    /// <summary>
    /// Handles the <see cref="HandledStripeWebhook.ChargeRefunded"/> event type from Stripe.
    /// </summary>
    /// <param name="parsedEvent"></param>
    public async Task HandleAsync(Event parsedEvent)
    {
        var charge = await _stripeEventService.GetCharge(parsedEvent, true, ["refunds"]);
        var parentTransaction = await _transactionRepository.GetByGatewayIdAsync(GatewayType.Stripe, charge.Id);
        if (parentTransaction == null)
        {
            // Attempt to create a transaction for the charge if it doesn't exist
            var (organizationId, userId, providerId) = await _stripeEventUtilityService.GetEntityIdsFromChargeAsync(charge);
            var tx = _stripeEventUtilityService.FromChargeToTransaction(charge, organizationId, userId, providerId);
            try
            {
                parentTransaction = await _transactionRepository.CreateAsync(tx);
            }
            catch (SqlException e) when (e.Number == 547) // FK constraint violation
            {
                _logger.LogWarning(
                    "Charge refund could not create transaction as entity may have been deleted. {ChargeId}",
                    charge.Id);
                return;
            }
        }

        var amountRefunded = charge.AmountRefunded / 100M;

        if (parentTransaction.Refunded.GetValueOrDefault() ||
            parentTransaction.RefundedAmount.GetValueOrDefault() >= amountRefunded)
        {
            _logger.LogWarning(
                "Charge refund amount doesn't match parent transaction's amount or parent has already been refunded. {ChargeId}",
                charge.Id);
            return;
        }

        parentTransaction.RefundedAmount = amountRefunded;
        if (charge.Refunded)
        {
            parentTransaction.Refunded = true;
        }

        await _transactionRepository.ReplaceAsync(parentTransaction);

        foreach (var refund in charge.Refunds)
        {
            var refundTransaction = await _transactionRepository.GetByGatewayIdAsync(
                GatewayType.Stripe, refund.Id);
            if (refundTransaction != null)
            {
                continue;
            }

            await _transactionRepository.CreateAsync(new Transaction
            {
                Amount = refund.Amount / 100M,
                CreationDate = refund.Created,
                OrganizationId = parentTransaction.OrganizationId,
                UserId = parentTransaction.UserId,
                ProviderId = parentTransaction.ProviderId,
                Type = TransactionType.Refund,
                Gateway = GatewayType.Stripe,
                GatewayId = refund.Id,
                PaymentMethodType = parentTransaction.PaymentMethodType,
                Details = parentTransaction.Details
            });
        }
    }
}
