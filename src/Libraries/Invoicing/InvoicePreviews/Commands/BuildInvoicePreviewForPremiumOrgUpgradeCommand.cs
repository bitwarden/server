using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Payment.Models;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Invoicing.InvoicePreviews.Models;
using Stripe;

namespace Bit.Invoicing.InvoicePreviews.Commands;

public interface IBuildInvoicePreviewForPremiumOrgUpgradeCommand
{
    /// <summary>
    /// Previews upgrading a user's Premium subscription to an organization plan: one prorated seat, credited
    /// for the unused Premium term, with tax.
    /// </summary>
    Task<InvoicePreview> Run(User user, PlanType targetPlanType, BillingAddress billingAddress);
}

public class BuildInvoicePreviewForPremiumOrgUpgradeCommand(
    IPricingClient pricingClient,
    IStripeAdapter stripeAdapter,
    IInvoicePreviewService invoicePreviewService) : IBuildInvoicePreviewForPremiumOrgUpgradeCommand
{
    public async Task<InvoicePreview> Run(User user, PlanType targetPlanType, BillingAddress billingAddress)
    {
        // The upgrade is annual-only.
        if (targetPlanType is not (PlanType.FamiliesAnnually or PlanType.TeamsAnnually or PlanType.EnterpriseAnnually))
        {
            throw new BadRequestException($"Cannot upgrade Premium subscription to {targetPlanType}.");
        }

        if (user is not { Premium: true, GatewaySubscriptionId: not null and not "", GatewayCustomerId: not null and not "" })
        {
            throw new BadRequestException("User does not have an active Premium subscription.");
        }

        var currentSubscription = await stripeAdapter.GetSubscriptionAsync(user.GatewaySubscriptionId);

        var premiumPlans = await pricingClient.ListPremiumPlans();
        var passwordManagerItem = currentSubscription.Items.Data.FirstOrDefault(item =>
            premiumPlans.Any(plan => plan.Seat.StripePriceId == item.Price.Id));

        if (passwordManagerItem == null)
        {
            throw new BadRequestException("Premium subscription password manager item not found.");
        }

        var usersPremiumPlan = premiumPlans.First(plan => plan.Seat.StripePriceId == passwordManagerItem.Price.Id);
        var targetPlan = await pricingClient.GetPlanOrThrow(targetPlanType);

        var subscriptionItems = new List<InvoiceSubscriptionDetailsItemOptions>();

        // Additional Premium storage does not carry over to the organization plan, so drop it from the preview.
        var storageItem = currentSubscription.Items.Data.FirstOrDefault(item =>
            item.Price.Id == usersPremiumPlan.Storage.StripePriceId);
        if (storageItem != null)
        {
            subscriptionItems.Add(new InvoiceSubscriptionDetailsItemOptions
            {
                Id = storageItem.Id,
                Deleted = true
            });
        }

        // Seats are always 1 on an upgrade: the upgrading user is the organization's only member.
        subscriptionItems.Add(new InvoiceSubscriptionDetailsItemOptions
        {
            Id = passwordManagerItem.Id,
            Price = targetPlan.HasNonSeatBasedPasswordManagerPlan()
                ? targetPlan.PasswordManager.StripePlanId
                : targetPlan.PasswordManager.StripeSeatPlanId,
            Quantity = 1
        });

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
                // AlwaysInvoice bills the switch immediately, so every line on the preview is a proration.
                ProrationBehavior = StripeConstants.ProrationBehavior.AlwaysInvoice
            }
        };

        try
        {
            return await invoicePreviewService.GetInvoicePreviewAsync(
                options, ResolvePlanTier(targetPlan.ProductTier), PlanCadenceType.Annually);
        }
        catch (StripeException stripeException)
            when (stripeException.StripeError?.Code == StripeConstants.ErrorCodes.CustomerTaxLocationInvalid)
        {
            // Caller-controlled input, so a 400 rather than a 500.
            throw new BadRequestException(
                "Your location wasn't recognized. Please ensure your country and postal code are valid and try again.");
        }
    }

    private static PlanTierType ResolvePlanTier(ProductTierType productTier) => productTier switch
    {
        ProductTierType.Families => PlanTierType.Families,
        ProductTierType.Teams => PlanTierType.Teams,
        ProductTierType.Enterprise => PlanTierType.Enterprise,
        _ => throw new ConflictException(
            message: $"Plan tier ({productTier}) has no cart to preview for a Premium upgrade.")
    };
}
