using Bit.Core.Billing.Commands;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Payment.Models;
using Bit.Core.Billing.Services;
using Bit.Core.Billing.Tax.Utilities;
using Bit.Core.Entities;
using Bit.Core.Services;
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
    IFeatureService featureService,
    ILogger<UpdateBillingAddressCommand> logger,
    ISubscriberService subscriberService,
    IStripeAdapter stripeAdapter) : BaseBillingCommand<UpdateBillingAddressCommand>(logger), IUpdateBillingAddressCommand
{
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
                    Expand = ["subscriptions"]
                });

        await EnableAutomaticTaxAsync(subscriber, customer);

        return BillingAddress.From(customer.Address);
    }

    private async Task<BillingCommandResult<BillingAddress>> UpdateBusinessBillingAddressAsync(
        ISubscriber subscriber,
        BillingAddress billingAddress)
    {
        var determinedTaxExemptStatus = await GetDeterminedTaxExemptStatusAsync(subscriber.GatewayCustomerId!, billingAddress.Country);

        var customer = await stripeAdapter.UpdateCustomerAsync(subscriber.GatewayCustomerId,
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
                Expand = ["subscriptions", "tax_ids"],
                TaxExempt = determinedTaxExemptStatus
            });

        await EnableAutomaticTaxAsync(subscriber, customer);

        var deleteExistingTaxIds = customer.TaxIds?.Any() ?? false
            ? customer.TaxIds.Select(taxId => stripeAdapter.DeleteTaxIdAsync(customer.Id, taxId.Id)).ToList()
            : [];

        if (billingAddress.TaxId == null)
        {
            await Task.WhenAll(deleteExistingTaxIds);
            return BillingAddress.From(customer.Address);
        }

        var updatedTaxId = await stripeAdapter.CreateTaxIdAsync(customer.Id,
            new TaxIdCreateOptions { Type = billingAddress.TaxId.Code, Value = billingAddress.TaxId.Value });

        if (billingAddress.TaxId.Code == StripeConstants.TaxIdType.SpanishNIF)
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


    private async Task<string> GetDeterminedTaxExemptStatusAsync(string customerId, string? billingCountry)
    {
        var existingCustomer = await stripeAdapter.GetCustomerAsync(customerId);
        return TaxHelpers.DetermineTaxExemptStatus(billingCountry, existingCustomer.TaxExempt);
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
                if (featureService.IsEnabled(FeatureFlagKeys.PM32645_DeferPriceMigrationToRenewal))
                {
                    var schedules = await stripeAdapter.ListSubscriptionSchedulesAsync(
                        new SubscriptionScheduleListOptions { Customer = subscription.CustomerId });

                    var activeSchedule = schedules.Data.FirstOrDefault(s =>
                        s.SubscriptionId == subscription.Id
                        && s.Status == StripeConstants.SubscriptionScheduleStatus.Active);

                    if (activeSchedule != null)
                    {
                        var now = DateTime.UtcNow;
                        var phases = new List<SubscriptionSchedulePhaseOptions>();

                        for (var i = 0; i < activeSchedule.Phases.Count; i++)
                        {
                            var phase = activeSchedule.Phases[i];

                            if (phase.EndDate <= now)
                            {
                                continue;
                            }

                            var discountConsumed = i > 0 && activeSchedule.Phases[i - 1].EndDate <= now;

                            phases.Add(new SubscriptionSchedulePhaseOptions
                            {
                                StartDate = phase.StartDate,
                                EndDate = phase.EndDate,
                                Items = phase.Items.Select(item => new SubscriptionSchedulePhaseItemOptions
                                {
                                    Price = item.PriceId,
                                    Quantity = item.Quantity
                                }).ToList(),
                                Discounts = discountConsumed
                                    ? []
                                    : phase.Discounts?.Select(d => new SubscriptionSchedulePhaseDiscountOptions
                                    {
                                        Coupon = d.CouponId
                                    }).ToList(),
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
