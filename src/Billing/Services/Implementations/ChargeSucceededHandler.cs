using Bit.Billing.Constants;
using Bit.Billing.Controllers;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Stripe;
using Event = Stripe.Event;

namespace Bit.Billing.Services.Implementations;

public class ChargeSucceededHandler : StripeWebhookHandler
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly ILogger<StripeController> _logger;
    private readonly IStripeEventService _stripeEventService;

    public ChargeSucceededHandler(
        ITransactionRepository transactionRepository,
        ILogger<StripeController> logger,
        IStripeEventService stripeEventService)
    {
        _transactionRepository = transactionRepository;
        _logger = logger;
        _stripeEventService = stripeEventService;
    }

    protected override bool CanHandle(Event parsedEvent)
    {
        return parsedEvent.Type.Equals(HandledStripeWebhook.ChargeSucceeded);
    }

    protected override async Task<IActionResult> ProcessEvent(Event parsedEvent)
    {
        // Handle ChargeSucceeded event
        var charge = await _stripeEventService.GetCharge(parsedEvent);
        var chargeTransaction = await _transactionRepository.GetByGatewayIdAsync(GatewayType.Stripe, charge.Id);
        if (chargeTransaction != null)
        {
            _logger.LogWarning("Charge success already processed. " + charge.Id);
            return new OkResult();
        }

        Tuple<Guid?, Guid?> ids = null;
        Subscription subscription = null;
        var subscriptionService = new SubscriptionService();

        if (charge.InvoiceId != null)
        {
            var invoiceService = new InvoiceService();
            var invoice = await invoiceService.GetAsync(charge.InvoiceId);
            if (invoice?.SubscriptionId != null)
            {
                subscription = await subscriptionService.GetAsync(invoice.SubscriptionId);
                ids = GetIdsFromMetaData(subscription?.Metadata);
            }
        }

        if (subscription == null || ids == null || (ids.Item1.HasValue && ids.Item2.HasValue))
        {
            var subscriptions = await subscriptionService.ListAsync(new SubscriptionListOptions
            {
                Customer = charge.CustomerId
            });
            foreach (var sub in subscriptions)
            {
                if (sub.Status != StripeSubscriptionStatus.Canceled && sub.Status != StripeSubscriptionStatus.IncompleteExpired)
                {
                    ids = GetIdsFromMetaData(sub.Metadata);
                    if (ids.Item1.HasValue || ids.Item2.HasValue)
                    {
                        subscription = sub;
                        break;
                    }
                }
            }
        }

        if (!ids.Item1.HasValue && !ids.Item2.HasValue)
        {
            _logger.LogWarning("Charge success has no subscriber ids. " + charge.Id);
            return new BadRequestResult();
        }

        var tx = new Transaction
        {
            Amount = charge.Amount / 100M,
            CreationDate = charge.Created,
            OrganizationId = ids.Item1,
            UserId = ids.Item2,
            Type = TransactionType.Charge,
            Gateway = GatewayType.Stripe,
            GatewayId = charge.Id
        };

        if (charge.Source != null && charge.Source is Card card)
        {
            tx.PaymentMethodType = PaymentMethodType.Card;
            tx.Details = $"{card.Brand}, *{card.Last4}";
        }
        else if (charge.Source != null && charge.Source is BankAccount bankAccount)
        {
            tx.PaymentMethodType = PaymentMethodType.BankAccount;
            tx.Details = $"{bankAccount.BankName}, *{bankAccount.Last4}";
        }
        else if (charge.Source != null && charge.Source is Source source)
        {
            if (source.Card != null)
            {
                tx.PaymentMethodType = PaymentMethodType.Card;
                tx.Details = $"{source.Card.Brand}, *{source.Card.Last4}";
            }
            else if (source.AchDebit != null)
            {
                tx.PaymentMethodType = PaymentMethodType.BankAccount;
                tx.Details = $"{source.AchDebit.BankName}, *{source.AchDebit.Last4}";
            }
            else if (source.AchCreditTransfer != null)
            {
                tx.PaymentMethodType = PaymentMethodType.BankAccount;
                tx.Details = $"ACH => {source.AchCreditTransfer.BankName}, " +
                    $"{source.AchCreditTransfer.AccountNumber}";
            }
        }
        else if (charge.PaymentMethodDetails != null)
        {
            if (charge.PaymentMethodDetails.Card != null)
            {
                tx.PaymentMethodType = PaymentMethodType.Card;
                tx.Details = $"{charge.PaymentMethodDetails.Card.Brand?.ToUpperInvariant()}, " +
                    $"*{charge.PaymentMethodDetails.Card.Last4}";
            }
            else if (charge.PaymentMethodDetails.AchDebit != null)
            {
                tx.PaymentMethodType = PaymentMethodType.BankAccount;
                tx.Details = $"{charge.PaymentMethodDetails.AchDebit.BankName}, " +
                    $"*{charge.PaymentMethodDetails.AchDebit.Last4}";
            }
            else if (charge.PaymentMethodDetails.AchCreditTransfer != null)
            {
                tx.PaymentMethodType = PaymentMethodType.BankAccount;
                tx.Details = $"ACH => {charge.PaymentMethodDetails.AchCreditTransfer.BankName}, " +
                    $"{charge.PaymentMethodDetails.AchCreditTransfer.AccountNumber}";
            }
        }

        if (!tx.PaymentMethodType.HasValue)
        {
            _logger.LogWarning("Charge success from unsupported source/method. " + charge.Id);
            return new OkResult();
        }

        try
        {
            await _transactionRepository.CreateAsync(tx);
        }
        // Catch foreign key violations because user/org could have been deleted.
        catch (SqlException e) when (e.Number == 547) { }

        return new OkResult(); ;
    }
}
