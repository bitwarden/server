using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Commands;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Models;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Billing.Payment.Models;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Microsoft.Extensions.Logging;
using OneOf;
using Stripe;

namespace Bit.Core.Billing.Organizations.Commands;

using static StripeConstants;

public interface IPreviewOrganizationTaxCommand
{
    Task<BillingCommandResult<(decimal Tax, decimal Total)>> Run(
        User user,
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
    IStripeAdapter stripeAdapter,
    ISubscriptionDiscountService subscriptionDiscountService)
    : BaseBillingCommand<PreviewOrganizationTaxCommand>(logger), IPreviewOrganizationTaxCommand
{
    private readonly ILogger<PreviewOrganizationTaxCommand> _logger = logger;

    public Task<BillingCommandResult<(decimal Tax, decimal Total)>> Run(
        User user,
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
                    // System coupon takes precedence for standalone Secrets Manager purchases.
                    // Any user-provided coupons are ignored in this scenario.
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

                    // Validate all coupons at once. If all are eligible, apply them; otherwise skip gracefully.
                    // Only Families plans support user-provided coupons.
                    if (purchase is { Coupons.Length: > 0, Tier: ProductTierType.Families })
                    {
                        var trimmedCoupons = purchase.Coupons
                            .Where(c => !string.IsNullOrWhiteSpace(c))
                            .Select(c => c.Trim())
                            .ToArray();

                        if (trimmedCoupons.Length > 0)
                        {
                            var allValid = await subscriptionDiscountService.ValidateDiscountEligibilityForUserAsync(
                                user, trimmedCoupons, DiscountTierType.Families);

                            if (allValid)
                            {
                                options.Discounts = trimmedCoupons
                                    .Select(c => new InvoiceDiscountOptions { Coupon = c })
                                    .ToList();
                            }
                        }
                    }

                    break;
            }

            options.SubscriptionDetails = new InvoiceSubscriptionDetailsOptions { Items = items };

            var invoice = await stripeAdapter.CreateInvoicePreviewAsync(options);
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

                var invoice = await stripeAdapter.CreateInvoicePreviewAsync(options);
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

                var subscription = await stripeAdapter.GetSubscriptionAsync(organization.GatewaySubscriptionId,
                    new SubscriptionGetOptions
                    {
                        Expand = ["customer", "discounts.coupon.applies_to"]
                    });

                // Genuine org coupons (complimentary PM, SM-standalone) attach at the subscription level, not the
                // customer. The migration coupon lives on the schedule, not the live subscription, so it's excluded.
                var discount = subscription.Customer?.Discount ?? subscription.Discounts?.FirstOrDefault();

                if (discount != null)
                {
                    options.Discounts =
                    [
                        new InvoiceDiscountOptions { Coupon = discount.Coupon.Id }
                    ];
                }

                var currentPlan = await pricingClient.GetPlanOrThrow(organization.PlanType);
                var newPlan = await pricingClient.GetPlanOrThrow(planChange.PlanType);

                var subscriptionItemsByPriceId =
                    subscription.Items.ToDictionary(subscriptionItem => subscriptionItem.Price.Id);

                var items = new List<InvoiceSubscriptionDetailsItemOptions>();

                long quantity;

                if (currentPlan.HasNonSeatBasedPasswordManagerPlan() && !newPlan.HasNonSeatBasedPasswordManagerPlan())
                {
                    // The current plan doesn't have a per-seat subscription item to read a quantity from
                    // (e.g. upgrading from a flat-rate plan like Teams Starter), so fall back to the
                    // organization's occupied seat count instead of looking it up on the subscription.
                    quantity = (long)organization.Seats!;
                }
                else
                {
                    var passwordManagerPriceId = currentPlan.HasNonSeatBasedPasswordManagerPlan()
                        ? currentPlan.PasswordManager.StripePlanId
                        : currentPlan.PasswordManager.StripeSeatPlanId;

                    if (!subscriptionItemsByPriceId.TryGetValue(passwordManagerPriceId, out var passwordManagerSeats))
                    {
                        _logger.LogError(
                            "Organization {OrganizationId}'s subscription {SubscriptionId} does not contain a " +
                            "Password Manager line item matching its current plan's price {PriceId}",
                            organization.Id, organization.GatewaySubscriptionId, passwordManagerPriceId);

                        return new BadRequest(
                            "Your organization's subscription does not match its current plan. Please contact support for assistance.");
                    }

                    quantity = passwordManagerSeats.Quantity;
                }

                items.Add(new InvoiceSubscriptionDetailsItemOptions
                {
                    Price = newPlan.HasNonSeatBasedPasswordManagerPlan()
                        ? newPlan.PasswordManager.StripePlanId
                        : newPlan.PasswordManager.StripeSeatPlanId,
                    Quantity = quantity
                });

                // Match existing storage by the CURRENT plan's id (as PM seats/SM do), then re-price at the
                // new plan — storage ids can differ across the change (e.g. Families' personal-storage vs an
                // org plan's shared storage). Guard: some plans have no storage add-on, so the id can be null.
                if (!string.IsNullOrEmpty(currentPlan.PasswordManager.StripeStoragePlanId) &&
                    !string.IsNullOrEmpty(newPlan.PasswordManager.StripeStoragePlanId))
                {
                    var hasStorage =
                        subscriptionItemsByPriceId.TryGetValue(currentPlan.PasswordManager.StripeStoragePlanId,
                            out var storage);

                    if (hasStorage && storage != null)
                    {
                        items.Add(new InvoiceSubscriptionDetailsItemOptions
                        {
                            Price = newPlan.PasswordManager.StripeStoragePlanId,
                            Quantity = storage.Quantity
                        });
                    }
                }

                // Match existing SM items by the CURRENT plan's ids (as PM seats/storage do above), then re-price
                // at the new plan. Guard: SecretsManager is null for Families/Free (PlanAdapter maps it to null).
                if (currentPlan.SecretsManager != null && newPlan.SecretsManager != null)
                {
                    var hasSecretsManagerSeats = subscriptionItemsByPriceId.TryGetValue(
                        currentPlan.SecretsManager.StripeSeatPlanId,
                        out var secretsManagerSeats);

                    if (hasSecretsManagerSeats && secretsManagerSeats != null)
                    {
                        items.Add(new InvoiceSubscriptionDetailsItemOptions
                        {
                            Price = newPlan.SecretsManager.StripeSeatPlanId,
                            Quantity = secretsManagerSeats.Quantity
                        });

                        var hasServiceAccounts =
                            subscriptionItemsByPriceId.TryGetValue(currentPlan.SecretsManager.StripeServiceAccountPlanId,
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
                }

                options.SubscriptionDetails = new InvoiceSubscriptionDetailsOptions { Items = items };

                var invoice = await stripeAdapter.CreateInvoicePreviewAsync(options);
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

            var subscription = await stripeAdapter.GetSubscriptionAsync(organization.GatewaySubscriptionId,
                new SubscriptionGetOptions
                {
                    Expand = ["customer.tax_ids", "discounts.coupon.applies_to"]
                });

            var options = GetBaseOptions(subscription.Customer,
                organization.GetProductUsageType() == ProductUsageType.Business);

            // Prefer a customer discount, else the first subscription-level one (see plan-change overload).
            var discount = subscription.Customer?.Discount ?? subscription.Discounts?.FirstOrDefault();

            if (discount != null)
            {
                options.Discounts =
                [
                    new InvoiceDiscountOptions { Coupon = discount.Coupon.Id }
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

            var invoice = await stripeAdapter.CreateInvoicePreviewAsync(options);
            return GetAmounts(invoice);
        });

    private static (decimal, decimal) GetAmounts(Invoice invoice) => (
        Convert.ToDecimal(invoice.TotalTaxes.Sum(invoiceTotalTax => invoiceTotalTax.Amount)) / 100,
        Convert.ToDecimal(invoice.Total) / 100);

    private InvoiceCreatePreviewOptions GetBaseOptions(
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
