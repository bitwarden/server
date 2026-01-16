using Bit.Core.Billing.Commands;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Payment.Models;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Microsoft.Extensions.Logging;
using Stripe;

namespace Bit.Core.Billing.Premium.Commands;

/// <summary>
/// Previews the proration details for upgrading a Premium user subscription to an Organization
/// plan by using the Stripe API to create an invoice preview, prorated, for the upgrade.
/// </summary>
public interface IPreviewPremiumUpgradeProrationCommand
{
    /// <summary>
    /// Calculates the tax, total cost, and proration credit for upgrading a Premium subscription to an Organization plan.
    /// </summary>
    /// <param name="user">The user with an active Premium subscription.</param>
    /// <param name="targetProductTierType">The target organization tier (Families, Teams, or Enterprise).</param>
    /// <param name="billingAddress">The billing address for tax calculation.</param>
    /// <returns>A tuple containing the tax amount, total cost, and proration credit from unused Premium time.</returns>
    Task<BillingCommandResult<(decimal Tax, decimal Total, decimal Credit)>> Run(
        User user,
        ProductTierType targetProductTierType,
        BillingAddress billingAddress);
}

public class PreviewPremiumUpgradeProrationCommand(
    ILogger<PreviewPremiumUpgradeProrationCommand> logger,
    IPricingClient pricingClient,
    IStripeAdapter stripeAdapter)
    : BaseBillingCommand<PreviewPremiumUpgradeProrationCommand>(logger),
      IPreviewPremiumUpgradeProrationCommand
{
    public Task<BillingCommandResult<(decimal Tax, decimal Total, decimal Credit)>> Run(
        User user,
        ProductTierType targetProductTierType,
        BillingAddress billingAddress) => HandleAsync<(decimal, decimal, decimal)>(async () =>
    {
        if (user is not { Premium: true, GatewaySubscriptionId: not null and not "" })
        {
            return new BadRequest("User does not have an active Premium subscription.");
        }

        if (targetProductTierType is not (ProductTierType.Families or ProductTierType.Teams or ProductTierType.Enterprise))
        {
            return new BadRequest($"Cannot upgrade Premium subscription to {targetProductTierType} plan.");
        }

        // Convert ProductTierType to PlanType (for premium upgrade, the only choice is annual plans so we can assume that cadence)
        var targetPlanType = targetProductTierType switch
        {
            ProductTierType.Families => PlanType.FamiliesAnnually,
            ProductTierType.Teams => PlanType.TeamsAnnually,
            ProductTierType.Enterprise => PlanType.EnterpriseAnnually,
            _ => throw new InvalidOperationException($"Unexpected ProductTierType: {targetProductTierType}")
        };

        // Hardcode seats to 1 for upgrade flow
        const int seats = 1;

        var currentSubscription = await stripeAdapter.GetSubscriptionAsync(
            user.GatewaySubscriptionId,
            new SubscriptionGetOptions { Expand = ["customer"] });
        var premiumPlans = await pricingClient.ListPremiumPlans();
        var passwordManagerItem = currentSubscription.Items.Data.FirstOrDefault(i =>
            premiumPlans.Any(p => p.Seat.StripePriceId == i.Price.Id));

        if (passwordManagerItem == null)
        {
            return new BadRequest("Premium subscription password manager item not found.");
        }

        var usersPremiumPlan = premiumPlans.First(p => p.Seat.StripePriceId == passwordManagerItem.Price.Id);
        var targetPlan = await pricingClient.GetPlanOrThrow(targetPlanType);
        var subscriptionItems = new List<InvoiceSubscriptionDetailsItemOptions>
        {
            // Delete the user's specific password manager item
            new() { Id = passwordManagerItem.Id, Deleted = true }
        };
        var storageItem = currentSubscription.Items.Data.FirstOrDefault(i =>
            i.Price.Id == usersPremiumPlan.Storage.StripePriceId);

        // Delete the storage item if it exists for this user's plan
        if (storageItem != null)
        {
            subscriptionItems.Add(new InvoiceSubscriptionDetailsItemOptions
            {
                Id = storageItem.Id,
                Deleted = true
            });
        }

        if (targetPlan.HasNonSeatBasedPasswordManagerPlan())
        {
            subscriptionItems.Add(new InvoiceSubscriptionDetailsItemOptions
            {
                Price = targetPlan.PasswordManager.StripePlanId,
                Quantity = 1
            });
        }
        else
        {
            subscriptionItems.Add(new InvoiceSubscriptionDetailsItemOptions
            {
                Price = targetPlan.PasswordManager.StripeSeatPlanId,
                Quantity = seats
            });
        }

        var options = new InvoiceCreatePreviewOptions
        {
            AutomaticTax = new InvoiceAutomaticTaxOptions { Enabled = true },
            Currency = "usd",
            Customer = user.GatewayCustomerId,
            Subscription = user.GatewaySubscriptionId,
            CustomerDetails = new InvoiceCustomerDetailsOptions
            {
                Address = new AddressOptions
                {
                    Country = billingAddress.Country,
                    PostalCode = billingAddress.PostalCode
                }
            },
            SubscriptionDetails = new InvoiceSubscriptionDetailsOptions
            {
                Items = subscriptionItems,
                ProrationBehavior = StripeConstants.ProrationBehavior.AlwaysInvoice
            }
        };

        var invoicePreview = await stripeAdapter.CreateInvoicePreviewAsync(options);
        var amounts = GetAmounts(invoicePreview);

        return amounts;
    });

    private static (decimal, decimal, decimal) GetAmounts(Invoice invoicePreview) => (
        Convert.ToDecimal(invoicePreview.TotalTaxes.Sum(invoiceTotalTax => invoiceTotalTax.Amount)) / 100,
        Convert.ToDecimal(invoicePreview.Total) / 100,
        GetProrationCreditFromInvoice(invoicePreview));


    private static decimal GetProrationCreditFromInvoice(Invoice invoicePreview)
    {
        // Extract proration credit from negative line items (credits are negative in Stripe)
        var prorationCredit = invoicePreview.Lines?.Data?
            .Where(line => line.Amount < 0)
            .Sum(line => Math.Abs(line.Amount)) ?? 0; // Return the credit as positive number

        return Convert.ToDecimal(prorationCredit) / 100;
    }
}
