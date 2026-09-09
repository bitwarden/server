using Bit.Core.Billing.Commands;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Payment.Models;
using Bit.Core.Billing.Services;
using Bit.Core.Billing.Tax.Services;
using Bit.Core.Entities;
using Microsoft.Extensions.Logging;
using Stripe;

namespace Bit.Core.Billing.Payment.Commands;

public interface IUpdateBillingAddressCommand
{
    Task<BillingCommandResult<BillingAddress>> Run(
        ISubscriber subscriber,
        BillingAddress billingAddress);
}

public class UpdateBillingAddressCommand(
    ILogger<UpdateBillingAddressCommand> logger,
    ISubscriberService subscriberService,
    IStripeAdapter stripeAdapter,
    ITaxService taxService) : BaseBillingCommand<UpdateBillingAddressCommand>(logger), IUpdateBillingAddressCommand
{
    private readonly ILogger<UpdateBillingAddressCommand> _logger = logger;

    protected override Conflict DefaultConflict =>
        new("We had a problem updating your billing address. Please contact support for assistance.");

    public Task<BillingCommandResult<BillingAddress>> Run(
        ISubscriber subscriber,
        BillingAddress billingAddress) => HandleAsync(async () =>
    {
        if (string.IsNullOrEmpty(subscriber.GatewayCustomerId))
        {
            await subscriberService.CreateStripeCustomer(subscriber);
        }

        return subscriber.GetProductUsageType() switch
        {
            ProductUsageType.Personal => await UpdatePersonalBillingAddressAsync(subscriber, billingAddress),
            ProductUsageType.Business => await UpdateBusinessBillingAddressAsync(subscriber, billingAddress)
        };
    });

    private async Task<BillingCommandResult<BillingAddress>> UpdatePersonalBillingAddressAsync(
        ISubscriber subscriber,
        BillingAddress billingAddress)
    {
        var customer =
            await stripeAdapter.UpdateCustomerAsync(subscriber.GatewayCustomerId,
                new CustomerUpdateOptions
                {
                    Address = new AddressOptions
                    {
                        Country = billingAddress.Country,
                        PostalCode = billingAddress.PostalCode,
                        Line1 = billingAddress.Line1,
                        Line2 = billingAddress.Line2,
                        City = billingAddress.City,
                        State = billingAddress.State
                    },
                    Expand = ["subscriptions", "subscriptions.data.test_clock", "subscriptions.data.discounts.source", "discount.source.coupon"]
                });

        await EnableAutomaticTaxAsync(subscriber, customer);

        return BillingAddress.From(customer.Address);
    }

    private async Task<BillingCommandResult<BillingAddress>> UpdateBusinessBillingAddressAsync(
        ISubscriber subscriber,
        BillingAddress billingAddress)
    {
        var updateOptions = new CustomerUpdateOptions
        {
            Address = new AddressOptions
            {
                Country = billingAddress.Country,
                PostalCode = billingAddress.PostalCode,
                Line1 = billingAddress.Line1,
                Line2 = billingAddress.Line2,
                City = billingAddress.City,
                State = billingAddress.State
            },
            Expand = ["subscriptions", "subscriptions.data.test_clock", "subscriptions.data.discounts.source", "tax_ids", "discount.source.coupon"]
        };

        var customer = await stripeAdapter.UpdateCustomerAsync(subscriber.GatewayCustomerId, updateOptions);

        await EnableAutomaticTaxAsync(subscriber, customer);

        var deleteExistingTaxIds = customer.TaxIds?.Any() ?? false
            ? customer.TaxIds.Select(taxId => stripeAdapter.DeleteTaxIdAsync(customer.Id, taxId.Id)).ToList()
            : [];

        if (billingAddress.TaxId == null)
        {
            await Task.WhenAll(deleteExistingTaxIds);
            return BillingAddress.From(customer.Address);
        }

        var derivedTaxIdCode = taxService.GetStripeTaxCode(billingAddress.Country, billingAddress.TaxId.Value);

        if (derivedTaxIdCode == null)
        {
            _logger.LogWarning(
                "Could not derive Stripe tax ID type for country {Country}; falling back to client-supplied type {TaxIdType}",
                billingAddress.Country, billingAddress.TaxId.Code);
        }

        var taxIdCode = derivedTaxIdCode ?? billingAddress.TaxId.Code;

        var updatedTaxId = await stripeAdapter.CreateTaxIdAsync(customer.Id,
            new TaxIdCreateOptions { Type = taxIdCode, Value = billingAddress.TaxId.Value });

        if (taxIdCode == StripeConstants.TaxIdType.SpanishNIF)
        {
            updatedTaxId = await stripeAdapter.CreateTaxIdAsync(customer.Id,
                new TaxIdCreateOptions
                {
                    Type = StripeConstants.TaxIdType.EUVAT,
                    Value = $"ES{billingAddress.TaxId.Value}"
                });
        }

        await Task.WhenAll(deleteExistingTaxIds);

        return BillingAddress.From(customer.Address, updatedTaxId);
    }

    private async Task EnableAutomaticTaxAsync(
        ISubscriber subscriber,
        Customer customer)
    {
        if (!string.IsNullOrEmpty(subscriber.GatewaySubscriptionId))
        {
            var subscription = customer.Subscriptions.FirstOrDefault(subscription =>
                subscription.Id == subscriber.GatewaySubscriptionId);

            if (subscription is { AutomaticTax.Enabled: false })
            {
                var schedules = await stripeAdapter.ListSubscriptionSchedulesAsync(
                    new SubscriptionScheduleListOptions { Customer = subscription.CustomerId });

                var activeSchedule = schedules.Data.FirstOrDefault(s =>
                    s.SubscriptionId == subscription.Id
                    && s.Status == StripeConstants.SubscriptionScheduleStatus.Active);

                if (activeSchedule != null)
                {
                    var now = subscription.TestClock?.FrozenTime ?? DateTime.UtcNow;

                    // subscription.Customer may be a bare id here (it comes from the customer's
                    // expanded subscriptions list); assign the already-fetched customer so the
                    // shared builder can read the customer-level coupon.
                    subscription.Customer = customer;

                    DiscountExtensions.RequireScheduleDiscountExpansions(subscription, _logger);

                    var phases = new List<SubscriptionSchedulePhaseOptions>();

                    foreach (var phase in activeSchedule.Phases)
                    {
                        if (phase.EndDate <= now)
                        {
                            continue;
                        }

                        var isFuture = phase.StartDate > now;

                        phases.Add(new SubscriptionSchedulePhaseOptions
                        {
                            StartDate = phase.StartDate,
                            EndDate = phase.EndDate,
                            Items = phase.Items.Select(item => new SubscriptionSchedulePhaseItemOptions
                            {
                                Price = item.PriceId,
                                Quantity = item.Quantity,
                                Discounts = DiscountExtensions.BuildPhaseItemLevelDiscounts(
                                    item.Discounts?.Select(d => d.CouponId) ?? [])
                            }).ToList(),
                            Discounts = isFuture
                                ? DiscountExtensions.BuildPhaseLevelDiscounts(
                                    subscription, [], preservedCouponIds: phase.Discounts?.Select(d => d.CouponId))
                                : DiscountExtensions.BuildCurrentPhaseDiscounts(subscription),
                            Metadata = phase.Metadata,
                            ProrationBehavior = phase.ProrationBehavior,
                            AutomaticTax = new SubscriptionSchedulePhaseAutomaticTaxOptions
                            {
                                Enabled = true
                            }
                        });
                    }

                    await stripeAdapter.UpdateSubscriptionScheduleAsync(activeSchedule.Id,
                        new SubscriptionScheduleUpdateOptions
                        {
                            DefaultSettings = new SubscriptionScheduleDefaultSettingsOptions
                            {
                                AutomaticTax = new SubscriptionScheduleDefaultSettingsAutomaticTaxOptions
                                {
                                    Enabled = true
                                }
                            },
                            Phases = phases
                        });
                    return;
                }

                await stripeAdapter.UpdateSubscriptionAsync(subscriber.GatewaySubscriptionId,
                    new SubscriptionUpdateOptions
                    {
                        AutomaticTax = new SubscriptionAutomaticTaxOptions { Enabled = true }
                    });
            }
        }
    }
}
