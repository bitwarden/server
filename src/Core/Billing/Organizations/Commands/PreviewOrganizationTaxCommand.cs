using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Commands;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Models;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Billing.Payment.Models;
using Bit.Core.Billing.Pricing;
using Bit.Core.Enums;
using Bit.Core.Services;
using Microsoft.Extensions.Logging;
using OneOf;
using Stripe;

namespace Bit.Core.Billing.Organizations.Commands;

using static Core.Constants;
using static StripeConstants;

public interface IPreviewOrganizationTaxCommand
{
    Task<BillingCommandResult<(decimal Tax, decimal Total)>> Run(
        OrganizationSubscriptionPurchase purchase,
        BillingAddress billingAddress);

    Task<BillingCommandResult<(decimal Tax, decimal Total)>> Run(
        Organization organization,
        OrganizationSubscriptionPlanChange planChange,
        BillingAddress billingAddress);

    Task<BillingCommandResult<(decimal Tax, decimal Total)>> Run(
        Organization organization,
        OrganizationSubscriptionUpdate update);
}

public class PreviewOrganizationTaxCommand(
    ILogger<PreviewOrganizationTaxCommand> logger,
    IPricingClient pricingClient,
    IStripeAdapter stripeAdapter)
    : BaseBillingCommand<PreviewOrganizationTaxCommand>(logger), IPreviewOrganizationTaxCommand
{
    public Task<BillingCommandResult<(decimal Tax, decimal Total)>> Run(
        OrganizationSubscriptionPurchase purchase,
        BillingAddress billingAddress)
        => HandleAsync<(decimal, decimal)>(async () =>
        {
            var plan = await pricingClient.GetPlanOrThrow(purchase.PlanType);

            var options = GetBaseOptions(billingAddress, purchase.Tier != ProductTierType.Families);

            var items = new List<InvoiceSubscriptionDetailsItemOptions>();

            switch (purchase)
            {
                case { PasswordManager.Sponsored: true }:
                    var sponsoredPlan = SponsoredPlans.Get(PlanSponsorshipType.FamiliesForEnterprise);
                    items.Add(new InvoiceSubscriptionDetailsItemOptions
                    {
                        Price = sponsoredPlan.StripePlanId,
                        Quantity = 1
                    });
                    break;

                case { SecretsManager.Standalone: true }:
                    items.AddRange([
                        new InvoiceSubscriptionDetailsItemOptions
                        {
                            Price = plan.PasswordManager.StripeSeatPlanId,
                            Quantity = purchase.PasswordManager.Seats
                        },
                        new InvoiceSubscriptionDetailsItemOptions
                        {
                            Price = plan.SecretsManager.StripeSeatPlanId,
                            Quantity = purchase.SecretsManager.Seats
                        }
                    ]);
                    options.Discounts =
                    [
                        new InvoiceDiscountOptions
                        {
                            Coupon = CouponIDs.SecretsManagerStandalone
                        }
                    ];
                    break;

                default:
                    items.Add(new InvoiceSubscriptionDetailsItemOptions
                    {
                        Price = plan.HasNonSeatBasedPasswordManagerPlan()
                            ? plan.PasswordManager.StripePlanId
                            : plan.PasswordManager.StripeSeatPlanId,
                        Quantity = purchase.PasswordManager.Seats
                    });

                    if (purchase.PasswordManager.AdditionalStorage > 0)
                    {
                        items.Add(new InvoiceSubscriptionDetailsItemOptions
                        {
                            Price = plan.PasswordManager.StripeStoragePlanId,
                            Quantity = purchase.PasswordManager.AdditionalStorage
                        });
                    }

                    if (purchase.SecretsManager is { Seats: > 0 })
                    {
                        items.Add(new InvoiceSubscriptionDetailsItemOptions
                        {
                            Price = plan.SecretsManager.StripeSeatPlanId,
                            Quantity = purchase.SecretsManager.Seats
                        });

                        if (purchase.SecretsManager.AdditionalServiceAccounts > 0)
                        {
                            items.Add(new InvoiceSubscriptionDetailsItemOptions
                            {
                                Price = plan.SecretsManager.StripeServiceAccountPlanId,
                                Quantity = purchase.SecretsManager.AdditionalServiceAccounts
                            });
                        }
                    }

                    break;
            }

            options.SubscriptionDetails = new InvoiceSubscriptionDetailsOptions { Items = items };

            var invoice = await stripeAdapter.InvoiceCreatePreviewAsync(options);
            return GetAmounts(invoice);
        });

    public Task<BillingCommandResult<(decimal Tax, decimal Total)>> Run(
        Organization organization,
        OrganizationSubscriptionPlanChange planChange,
        BillingAddress billingAddress)
        => HandleAsync<(decimal, decimal)>(async () =>
        {
            if (organization.PlanType.GetProductTier() == ProductTierType.Free)
            {
                var options = GetBaseOptions(billingAddress, planChange.Tier != ProductTierType.Families);

                var newPlan = await pricingClient.GetPlanOrThrow(planChange.PlanType);

                var quantity = newPlan.HasNonSeatBasedPasswordManagerPlan() ? 1 : 2;

                var items = new List<InvoiceSubscriptionDetailsItemOptions>
                {
                    new ()
                    {
                        Price = newPlan.HasNonSeatBasedPasswordManagerPlan()
                            ? newPlan.PasswordManager.StripePlanId
                            : newPlan.PasswordManager.StripeSeatPlanId,
                        Quantity = quantity
                    }
                };

                if (organization.UseSecretsManager && planChange.Tier != ProductTierType.Families)
                {
                    items.Add(new InvoiceSubscriptionDetailsItemOptions
                    {
                        Price = newPlan.SecretsManager.StripeSeatPlanId,
                        Quantity = 2
                    });
                }

                options.SubscriptionDetails = new InvoiceSubscriptionDetailsOptions { Items = items };

                var invoice = await stripeAdapter.InvoiceCreatePreviewAsync(options);
                return GetAmounts(invoice);
            }
            else
            {
                if (organization is not
                    {
                        GatewayCustomerId: not null,
                        GatewaySubscriptionId: not null
                    })
                {
                    return new BadRequest("Organization does not have a subscription.");
                }

                var options = GetBaseOptions(billingAddress, planChange.Tier != ProductTierType.Families);

                var subscription = await stripeAdapter.SubscriptionGetAsync(organization.GatewaySubscriptionId,
                    new SubscriptionGetOptions { Expand = ["customer"] });

                if (subscription.Customer.Discount != null)
                {
                    options.Discounts =
                    [
                        new InvoiceDiscountOptions { Coupon = subscription.Customer.Discount.Coupon.Id }
                    ];
                }

                var currentPlan = await pricingClient.GetPlanOrThrow(organization.PlanType);
                var newPlan = await pricingClient.GetPlanOrThrow(planChange.PlanType);

                var subscriptionItemsByPriceId =
                    subscription.Items.ToDictionary(subscriptionItem => subscriptionItem.Price.Id);

                var items = new List<InvoiceSubscriptionDetailsItemOptions>();

                var passwordManagerSeats = subscriptionItemsByPriceId[
                    currentPlan.HasNonSeatBasedPasswordManagerPlan()
                        ? currentPlan.PasswordManager.StripePlanId
                        : currentPlan.PasswordManager.StripeSeatPlanId];

                var quantity = currentPlan.HasNonSeatBasedPasswordManagerPlan() &&
                               !newPlan.HasNonSeatBasedPasswordManagerPlan()
                    ? (long)organization.Seats!
                    : passwordManagerSeats.Quantity;

                items.Add(new InvoiceSubscriptionDetailsItemOptions
                {
                    Price = newPlan.HasNonSeatBasedPasswordManagerPlan()
                        ? newPlan.PasswordManager.StripePlanId
                        : newPlan.PasswordManager.StripeSeatPlanId,
                    Quantity = quantity
                });

                var hasStorage =
                    subscriptionItemsByPriceId.TryGetValue(newPlan.PasswordManager.StripeStoragePlanId,
                        out var storage);

                if (hasStorage && storage != null)
                {
                    items.Add(new InvoiceSubscriptionDetailsItemOptions
                    {
                        Price = newPlan.PasswordManager.StripeStoragePlanId,
                        Quantity = storage.Quantity
                    });
                }

                var hasSecretsManagerSeats = subscriptionItemsByPriceId.TryGetValue(
                    newPlan.SecretsManager.StripeSeatPlanId,
                    out var secretsManagerSeats);

                if (hasSecretsManagerSeats && secretsManagerSeats != null)
                {
                    items.Add(new InvoiceSubscriptionDetailsItemOptions
                    {
                        Price = newPlan.SecretsManager.StripeSeatPlanId,
                        Quantity = secretsManagerSeats.Quantity
                    });

                    var hasServiceAccounts =
                        subscriptionItemsByPriceId.TryGetValue(newPlan.SecretsManager.StripeServiceAccountPlanId,
                            out var serviceAccounts);

                    if (hasServiceAccounts && serviceAccounts != null)
                    {
                        items.Add(new InvoiceSubscriptionDetailsItemOptions
                        {
                            Price = newPlan.SecretsManager.StripeServiceAccountPlanId,
                            Quantity = serviceAccounts.Quantity
                        });
                    }
                }

                options.SubscriptionDetails = new InvoiceSubscriptionDetailsOptions { Items = items };

                var invoice = await stripeAdapter.InvoiceCreatePreviewAsync(options);
                return GetAmounts(invoice);
            }
        });

    public Task<BillingCommandResult<(decimal Tax, decimal Total)>> Run(
        Organization organization,
        OrganizationSubscriptionUpdate update)
        => HandleAsync<(decimal, decimal)>(async () =>
        {
            if (organization is not
                {
                    GatewayCustomerId: not null,
                    GatewaySubscriptionId: not null
                })
            {
                return new BadRequest("Organization does not have a subscription.");
            }

            var subscription = await stripeAdapter.SubscriptionGetAsync(organization.GatewaySubscriptionId,
                new SubscriptionGetOptions { Expand = ["customer.tax_ids"] });

            var options = GetBaseOptions(subscription.Customer,
                organization.GetProductUsageType() == ProductUsageType.Business);

            if (subscription.Customer.Discount != null)
            {
                options.Discounts =
                [
                    new InvoiceDiscountOptions { Coupon = subscription.Customer.Discount.Coupon.Id }
                ];
            }

            var currentPlan = await pricingClient.GetPlanOrThrow(organization.PlanType);

            var items = new List<InvoiceSubscriptionDetailsItemOptions>();

            if (update.PasswordManager?.Seats != null)
            {
                items.Add(new InvoiceSubscriptionDetailsItemOptions
                {
                    Price = currentPlan.HasNonSeatBasedPasswordManagerPlan()
                        ? currentPlan.PasswordManager.StripePlanId
                        : currentPlan.PasswordManager.StripeSeatPlanId,
                    Quantity = update.PasswordManager.Seats
                });
            }

            if (update.PasswordManager?.AdditionalStorage is > 0)
            {
                items.Add(new InvoiceSubscriptionDetailsItemOptions
                {
                    Price = currentPlan.PasswordManager.StripeStoragePlanId,
                    Quantity = update.PasswordManager.AdditionalStorage
                });
            }

            if (update.SecretsManager?.Seats is > 0)
            {
                items.Add(new InvoiceSubscriptionDetailsItemOptions
                {
                    Price = currentPlan.SecretsManager.StripeSeatPlanId,
                    Quantity = update.SecretsManager.Seats
                });

                if (update.SecretsManager.AdditionalServiceAccounts is > 0)
                {
                    items.Add(new InvoiceSubscriptionDetailsItemOptions
                    {
                        Price = currentPlan.SecretsManager.StripeServiceAccountPlanId,
                        Quantity = update.SecretsManager.AdditionalServiceAccounts
                    });
                }
            }

            options.SubscriptionDetails = new InvoiceSubscriptionDetailsOptions { Items = items };

            var invoice = await stripeAdapter.InvoiceCreatePreviewAsync(options);
            return GetAmounts(invoice);
        });

    private static (decimal, decimal) GetAmounts(Invoice invoice) => (
        Convert.ToDecimal(invoice.TotalTaxes.Sum(invoiceTotalTax => invoiceTotalTax.Amount)) / 100,
        Convert.ToDecimal(invoice.Total) / 100);

    private static InvoiceCreatePreviewOptions GetBaseOptions(
        OneOf<Customer, BillingAddress> addressChoice,
        bool businessUse)
    {
        var country = addressChoice.Match(
            customer => customer.Address.Country,
            billingAddress => billingAddress.Country
        );

        var postalCode = addressChoice.Match(
            customer => customer.Address.PostalCode,
            billingAddress => billingAddress.PostalCode);

        var options = new InvoiceCreatePreviewOptions
        {
            AutomaticTax = new InvoiceAutomaticTaxOptions { Enabled = true },
            Currency = "usd",
            CustomerDetails = new InvoiceCustomerDetailsOptions
            {
                Address = new AddressOptions { Country = country, PostalCode = postalCode },
                TaxExempt = businessUse && country != CountryAbbreviations.UnitedStates
                    ? TaxExempt.Reverse
                    : TaxExempt.None
            }
        };

        var taxId = addressChoice.Match(
            customer =>
            {
                var taxId = customer.TaxIds?.FirstOrDefault();
                return taxId != null ? new TaxID(taxId.Type, taxId.Value) : null;
            },
            billingAddress => billingAddress.TaxId);

        if (taxId == null)
        {
            return options;
        }

        options.CustomerDetails.TaxIds =
        [
            new InvoiceCustomerDetailsTaxIdOptions { Type = taxId.Code, Value = taxId.Value }
        ];

        if (taxId.Code == TaxIdType.SpanishNIF)
        {
            options.CustomerDetails.TaxIds.Add(new InvoiceCustomerDetailsTaxIdOptions
            {
                Type = TaxIdType.EUVAT,
                Value = $"ES{taxId.Value}"
            });
        }

        return options;
    }
}
