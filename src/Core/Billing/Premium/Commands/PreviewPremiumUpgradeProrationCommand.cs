using Bit.Core.Billing.Commands;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Payment.Models;
using Bit.Core.Billing.Premium.Models;
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
    /// <param name="targetPlanType">The target organization plan type.</param>
    /// <param name="billingAddress">The billing address for tax calculation.</param>
    /// <returns>The proration details for the upgrade including costs, credits, tax, and time remaining.</returns>
    Task<BillingCommandResult<PremiumUpgradeProration>> Run(
        User user,
        PlanType targetPlanType,
        BillingAddress billingAddress);
}

public class PreviewPremiumUpgradeProrationCommand(
    ILogger<PreviewPremiumUpgradeProrationCommand> logger,
    IPricingClient pricingClient,
    IStripeAdapter stripeAdapter)
    : BaseBillingCommand<PreviewPremiumUpgradeProrationCommand>(logger),
      IPreviewPremiumUpgradeProrationCommand
{
    public Task<BillingCommandResult<PremiumUpgradeProration>> Run(
        User user,
        PlanType targetPlanType,
        BillingAddress billingAddress) => HandleAsync<PremiumUpgradeProration>(async () =>
    {
        if (user is not { Premium: true, GatewaySubscriptionId: not null and not "" })
        {
            return new BadRequest("User does not have an active Premium subscription.");
        }

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
        var subscriptionItems = new List<InvoiceSubscriptionDetailsItemOptions>();
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

        // Hardcode seats to 1 for upgrade flow
        if (targetPlan.HasNonSeatBasedPasswordManagerPlan())
        {
            subscriptionItems.Add(new InvoiceSubscriptionDetailsItemOptions
            {
                Id = passwordManagerItem.Id,
                Price = targetPlan.PasswordManager.StripePlanId,
                Quantity = 1
            });
        }
        else
        {
            subscriptionItems.Add(new InvoiceSubscriptionDetailsItemOptions
            {
                Id = passwordManagerItem.Id,
                Price = targetPlan.PasswordManager.StripeSeatPlanId,
                Quantity = 1
            });
        }

        var options = new InvoiceCreatePreviewOptions
        {
            AutomaticTax = new InvoiceAutomaticTaxOptions { Enabled = true },
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
        var proration = GetProration(invoicePreview, passwordManagerItem);

        return proration;
    });

    private static PremiumUpgradeProration GetProration(Invoice invoicePreview, SubscriptionItem passwordManagerItem) => new()
    {
        NewPlanProratedAmount = GetNewPlanProratedAmountFromInvoice(invoicePreview),
        Credit = GetProrationCreditFromInvoice(invoicePreview),
        Tax = Convert.ToDecimal(invoicePreview.TotalTaxes.Sum(invoiceTotalTax => invoiceTotalTax.Amount)) / 100,
        Total = Convert.ToDecimal(invoicePreview.Total) / 100,
        // Use invoice periodEnd here instead of UtcNow so that testing with Stripe time clocks works correctly. And if there is no test clock,
        // (like in production), the previewInvoice's periodEnd is the same as UtcNow anyway because of the proration behavior (always_invoice)
        NewPlanProratedMonths = CalculateNewPlanProratedMonths(invoicePreview.PeriodEnd, passwordManagerItem.CurrentPeriodEnd)
    };

    private static decimal GetProrationCreditFromInvoice(Invoice invoicePreview)
    {
        // Extract proration credit from negative line items (credits are negative in Stripe)
        var prorationCredit = invoicePreview.Lines?.Data?
            .Where(line => line.Amount < 0)
            .Sum(line => Math.Abs(line.Amount)) ?? 0; // Return the credit as positive number

        return Convert.ToDecimal(prorationCredit) / 100;
    }

    private static decimal GetNewPlanProratedAmountFromInvoice(Invoice invoicePreview)
    {
        // The target plan's prorated upgrade amount should be the only positive-valued line item
        var proratedTotal = invoicePreview.Lines?.Data?
            .Where(line => line.Amount > 0)
            .Sum(line => line.Amount) ?? 0;

        return Convert.ToDecimal(proratedTotal) / 100;
    }

    private static int CalculateNewPlanProratedMonths(DateTime invoicePeriodEnd, DateTime currentPeriodEnd)
    {
        var daysInProratedPeriod = (currentPeriodEnd - invoicePeriodEnd).TotalDays;

        // Round to nearest month (30-day periods)
        // 1-14 days = 1 month, 15-44 days = 1 month, 45-74 days = 2 months, etc.
        // Minimum is always 1 month (never returns 0)
        // Use MidpointRounding.AwayFromZero to round 0.5 up to 1
        var months = (int)Math.Round(daysInProratedPeriod / 30, MidpointRounding.AwayFromZero);
        return Math.Max(1, months);
    }
}
