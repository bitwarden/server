using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Exceptions;
using Bit.Invoicing.InvoicePreviews;
using Bit.Invoicing.InvoicePreviews.Models;
using Bit.Subscriptions.User.Models.Requests;
using Microsoft.Extensions.Logging;
using Stripe;
using UserEntity = Bit.Core.Entities.User;

namespace Bit.Subscriptions.User.Commands;

internal interface IPreviewPremiumUpgradeCommand
{
    Task<InvoicePreview> Run(UserEntity user, PreviewPremiumUpgradeRequest request);
}

internal sealed class PreviewPremiumUpgradeCommand(
    ILogger<PreviewPremiumUpgradeCommand> logger,
    IPricingClient pricingClient,
    IStripeAdapter stripeAdapter,
    IInvoicePreviewService invoicePreviewService) : IPreviewPremiumUpgradeCommand
{
    private const string InvalidSubscriptionMessage =
        "Subscription is in an invalid state. Please contact support for assistance.";

    public async Task<InvoicePreview> Run(UserEntity user, PreviewPremiumUpgradeRequest request)
    {
        var (targetPlanType, targetPlanTier) = ResolveTargetPlan(request.TargetProductTierType);
        var billingAddress = ResolveBillingAddress(request.BillingAddress);

        if (!user.Premium)
        {
            throw new BadRequestException("User does not have an active Premium subscription.");
        }

        if (string.IsNullOrEmpty(user.GatewaySubscriptionId))
        {
            logger.LogError("Premium user ({UserId}) has no gateway subscription to preview an upgrade for", user.Id);
            throw new ConflictException(InvalidSubscriptionMessage);
        }

        var subscription = await stripeAdapter.GetSubscriptionAsync(user.GatewaySubscriptionId);
        var premiumPlans = await pricingClient.ListPremiumPlans();

        var passwordManagerItem = subscription.Items.Data.FirstOrDefault(item =>
            premiumPlans.Any(plan => plan.Seat.StripePriceId == item.Price.Id));
        if (passwordManagerItem == null)
        {
            logger.LogError("Subscription ({SubscriptionId}) for user ({UserId}) has no Premium password manager item",
                subscription.Id, user.Id);
            throw new ConflictException(InvalidSubscriptionMessage);
        }

        var premiumPlan = premiumPlans.First(plan => plan.Seat.StripePriceId == passwordManagerItem.Price.Id);
        var targetPlan = await pricingClient.GetPlanOrThrow(targetPlanType);

        var items = new List<InvoiceSubscriptionDetailsItemOptions>();

        var storageItem = subscription.Items.Data.FirstOrDefault(item => item.Price.Id == premiumPlan.Storage.StripePriceId);
        if (storageItem != null)
        {
            items.Add(new InvoiceSubscriptionDetailsItemOptions { Id = storageItem.Id, Deleted = true });
        }

        items.Add(new InvoiceSubscriptionDetailsItemOptions
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
            CustomerDetails = new InvoiceCustomerDetailsOptions { Address = billingAddress },
            SubscriptionDetails = new InvoiceSubscriptionDetailsOptions
            {
                Items = items,
                ProrationBehavior = StripeConstants.ProrationBehavior.AlwaysInvoice
            }
        };

        try
        {
            return await invoicePreviewService.GetInvoicePreviewAsync(options, targetPlanTier, PlanCadenceType.Annually);
        }
        catch (StripeException stripeException)
            when (stripeException.StripeError?.Code == StripeConstants.ErrorCodes.CustomerTaxLocationInvalid)
        {
            throw new BadRequestException(
                "Your location wasn't recognized. Please ensure your country and postal code are valid and try again.");
        }
    }

    private static (PlanType PlanType, PlanTierType PlanTier) ResolveTargetPlan(ProductTierType targetProductTierType) =>
        targetProductTierType switch
        {
            ProductTierType.Families => (PlanType.FamiliesAnnually, PlanTierType.Families),
            ProductTierType.Teams => (PlanType.TeamsAnnually, PlanTierType.Teams),
            ProductTierType.Enterprise => (PlanType.EnterpriseAnnually, PlanTierType.Enterprise),
            _ => throw new BadRequestException(
                nameof(PreviewPremiumUpgradeRequest.TargetProductTierType),
                $"Cannot upgrade Premium subscription to {targetProductTierType} plan.")
        };

    private static AddressOptions ResolveBillingAddress(BillingAddressRequest? billingAddress)
    {
        if (billingAddress is null)
        {
            throw new BadRequestException(
                nameof(PreviewPremiumUpgradeRequest.BillingAddress), "The BillingAddress field is required.");
        }

        if (string.IsNullOrWhiteSpace(billingAddress.Country) || billingAddress.Country.Length != 2)
        {
            throw new BadRequestException(
                nameof(BillingAddressRequest.Country), "Country code must be 2 characters long.");
        }

        if (string.IsNullOrWhiteSpace(billingAddress.PostalCode))
        {
            throw new BadRequestException(
                nameof(BillingAddressRequest.PostalCode), "The PostalCode field is required.");
        }

        return new AddressOptions { Country = billingAddress.Country, PostalCode = billingAddress.PostalCode };
    }
}
