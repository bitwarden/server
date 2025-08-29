using Bit.Core.Billing.Caches;
using Bit.Core.Billing.Commands;
using Bit.Core.Billing.Payment.Models;
using Bit.Core.Entities;
using Bit.Core.Services;
using Microsoft.Extensions.Logging;
using Stripe;

namespace Bit.Core.Billing.Payment.Commands;

public interface IVerifyBankAccountCommand
{
    Task<BillingCommandResult<MaskedPaymentMethod>> Run(
        ISubscriber subscriber,
        string descriptorCode);
}

public class VerifyBankAccountCommand(
    ILogger<VerifyBankAccountCommand> logger,
    ISetupIntentCache setupIntentCache,
    IStripeAdapter stripeAdapter) : BaseBillingCommand<VerifyBankAccountCommand>(logger), IVerifyBankAccountCommand
{
    private readonly ILogger<VerifyBankAccountCommand> _logger = logger;

    protected override Conflict DefaultConflict
        => new("We had a problem verifying your bank account. Please contact support for assistance.");

    public Task<BillingCommandResult<MaskedPaymentMethod>> Run(
        ISubscriber subscriber,
        string descriptorCode) => HandleAsync<MaskedPaymentMethod>(async () =>
    {
        var setupIntentId = await setupIntentCache.GetSetupIntentIdForSubscriber(subscriber.Id);

        if (string.IsNullOrEmpty(setupIntentId))
        {
            _logger.LogError(
                "{Command}: Could not find setup intent to verify subscriber's ({SubscriberID}) bank account",
                CommandName, subscriber.Id);
            return DefaultConflict;
        }

        await stripeAdapter.SetupIntentVerifyMicroDeposit(setupIntentId,
            new SetupIntentVerifyMicrodepositsOptions { DescriptorCode = descriptorCode });

        var setupIntent = await stripeAdapter.SetupIntentGet(setupIntentId,
            new SetupIntentGetOptions { Expand = ["payment_method"] });

        var paymentMethod = await stripeAdapter.PaymentMethodAttachAsync(setupIntent.PaymentMethodId,
            new PaymentMethodAttachOptions { Customer = subscriber.GatewayCustomerId });

        await stripeAdapter.CustomerUpdateAsync(subscriber.GatewayCustomerId,
            new CustomerUpdateOptions
            {
                InvoiceSettings = new CustomerInvoiceSettingsOptions
                {
                    DefaultPaymentMethod = setupIntent.PaymentMethodId
                }
            });

        return MaskedPaymentMethod.From(paymentMethod.UsBankAccount);
    });
}
