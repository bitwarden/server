using Bit.Billing.Constants;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Microsoft.AspNetCore.Mvc;
using Event = Stripe.Event;

namespace Bit.Billing.Services.Implementations;

public class ChargeRefundedHandler : StripeWebhookHandler
{
    private readonly IStripeEventService _stripeEventService;
    private readonly ITransactionRepository _transactionRepository;
    private readonly ILogger<ChargeSucceededHandler> _logger;
    public ChargeRefundedHandler(IStripeEventService stripeEventService,
        ITransactionRepository transactionRepository,
        ILogger<ChargeSucceededHandler> logger)
    {
        _stripeEventService = stripeEventService;
        _transactionRepository = transactionRepository;
        _logger = logger;
    }
    protected override bool CanHandle(Event parsedEvent)
    {
        return parsedEvent.Type.Equals(HandledStripeWebhook.ChargeSucceeded);
    }

    protected override async Task<IActionResult> ProcessEvent(Event parsedEvent)
    {
        var charge = await _stripeEventService.GetCharge(parsedEvent);
        var chargeTransaction = await _transactionRepository.GetByGatewayIdAsync(
            GatewayType.Stripe, charge.Id);
        if (chargeTransaction == null)
        {
            throw new Exception("Cannot find refunded charge. " + charge.Id);
        }

        var amountRefunded = charge.AmountRefunded / 100M;

        if (!chargeTransaction.Refunded.GetValueOrDefault() &&
            chargeTransaction.RefundedAmount.GetValueOrDefault() < amountRefunded)
        {
            chargeTransaction.RefundedAmount = amountRefunded;
            if (charge.Refunded)
            {
                chargeTransaction.Refunded = true;
            }
            await _transactionRepository.ReplaceAsync(chargeTransaction);

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
                    OrganizationId = chargeTransaction.OrganizationId,
                    UserId = chargeTransaction.UserId,
                    Type = TransactionType.Refund,
                    Gateway = GatewayType.Stripe,
                    GatewayId = refund.Id,
                    PaymentMethodType = chargeTransaction.PaymentMethodType,
                    Details = chargeTransaction.Details
                });
            }
        }
        else
        {
            _logger.LogWarning("Charge refund amount doesn't seem correct. " + charge.Id);
        }

        return new OkResult(); ;
    }
}
