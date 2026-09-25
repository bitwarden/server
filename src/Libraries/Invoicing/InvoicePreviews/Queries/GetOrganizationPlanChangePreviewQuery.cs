using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Exceptions;
using Bit.Invoicing.InvoicePreviews.Models;
using Microsoft.Extensions.Logging;
using Stripe;

namespace Bit.Invoicing.InvoicePreviews.Queries;

using static StripeConstants;
using Plan = Core.Models.StaticStore.Plan;

public interface IGetOrganizationPlanChangePreviewQuery
{
    /// <summary>
    /// Builds the <see cref="InvoicePreview"/> (cart) for changing an organization's plan. For an org with a
    /// live subscription the preview is prorated against it — supplying the subscription and
    /// <see cref="ProrationBehavior.AlwaysInvoice"/> so Stripe computes the mid-cycle credit — while an org with
    /// no subscription (e.g. upgrading from Free) gets a fresh full-price preview. Tax is estimated from the
    /// plan change's country and postal code, so no Stripe customer is required.
    /// </summary>
    /// <param name="organization">The organization for which to build the plan change preview.</param>
    /// <param name="planChange">The target plan change to preview.</param>
    /// <returns>The <see cref="InvoicePreview"/> for the plan change.</returns>
    Task<InvoicePreview> Run(Organization organization, OrganizationPlanChange planChange);
}

public class GetOrganizationPlanChangePreviewQuery(
    ILogger<GetOrganizationPlanChangePreviewQuery> logger,
    IPricingClient pricingClient,
    IStripeAdapter stripeAdapter,
    IInvoicePreviewService invoicePreviewService) : IGetOrganizationPlanChangePreviewQuery
{
    /// <inheritdoc />
    public async Task<InvoicePreview> Run(Organization organization, OrganizationPlanChange planChange)
    {
        var newPlan = await pricingClient.GetPlanOrThrow(ResolvePlanType(planChange));

        if (organization.UseSecretsManager && !newPlan.SupportsSecretsManager)
        {
            throw new BadRequestException("The selected plan does not support Secrets Manager.");
        }

        var billingAddress = ResolveBillingAddress(planChange.Country, planChange.PostalCode);

        var options = (
                HasSubscription: !string.IsNullOrEmpty(organization.GatewaySubscriptionId),
                IsFreeOrganization: organization.PlanType == PlanType.Free)
            switch
            {
                { HasSubscription: true } => await BuildPlanChangeOptionsAsync(organization, newPlan),
                { IsFreeOrganization: true } => await BuildPurchaseOptionsAsync(organization, newPlan),
                _ => throw new BadRequestException(
                    "Your organization has no subscription to preview a plan change against. Please contact support for assistance.")
            };

        options.AutomaticTax = new InvoiceAutomaticTaxOptions { Enabled = true };
        options.CustomerDetails = new InvoiceCustomerDetailsOptions
        {
            Address = billingAddress
        };

        return await invoicePreviewService.GetInvoicePreviewAsync(options, planChange.Tier, planChange.Cadence);
    }

    private static PlanType ResolvePlanType(OrganizationPlanChange planChange) =>
        planChange.Tier switch
        {
            PlanTierType.Families => PlanType.FamiliesAnnually,
            PlanTierType.Teams => planChange.Cadence == PlanCadenceType.Monthly
                ? PlanType.TeamsMonthly
                : PlanType.TeamsAnnually,
            PlanTierType.Enterprise => planChange.Cadence == PlanCadenceType.Monthly
                ? PlanType.EnterpriseMonthly
                : PlanType.EnterpriseAnnually,
            _ => throw new BadRequestException($"Cannot change an organization to the {planChange.Tier} tier.")
        };

    private static AddressOptions ResolveBillingAddress(string? country, string? postalCode)
    {
        if (string.IsNullOrWhiteSpace(country) || country.Length != 2)
        {
            throw new BadRequestException(nameof(country), "Country code must be 2 characters long.");
        }

        if (string.IsNullOrWhiteSpace(postalCode))
        {
            throw new BadRequestException(nameof(postalCode), "The PostalCode field is required.");
        }

        return new AddressOptions { Country = country, PostalCode = postalCode };
    }

    /// <summary>
    /// Builds the preview options for an organization that has a live subscription and is changing to a new plan.
    /// </summary>
    /// <param name="organization">The organization for which to build the plan change options.</param>
    /// <param name="newPlan">The new plan to which the organization is changing.</param>
    /// <returns>The preview options for the plan change.</returns>
    private async Task<InvoiceCreatePreviewOptions> BuildPlanChangeOptionsAsync(Organization organization, Plan newPlan)
    {
        var currentPlan = await pricingClient.GetPlanOrThrow(organization.PlanType);

        if (currentPlan.UpgradeSortOrder > newPlan.UpgradeSortOrder)
        {
            throw new BadRequestException("You can't downgrade your organization's plan.");
        }

        var subscription = await GetSubscriptionOrThrowAsync(organization);
        var itemsByPriceId = subscription.Items.ToDictionary(item => item.Price.Id);

        var changeSet = BuildPlanChangeSet(organization, currentPlan, newPlan);
        var items = changeSet.Changes
            .Select(change => ToPreviewItem(change, itemsByPriceId, organization))
            .ToList();

        return new InvoiceCreatePreviewOptions
        {
            Customer = organization.GatewayCustomerId,
            Subscription = organization.GatewaySubscriptionId,
            SubscriptionDetails = new InvoiceSubscriptionDetailsOptions
            {
                Items = items, ProrationBehavior = ProrationBehavior.AlwaysInvoice
            }
        };
    }

    private async Task<Subscription> GetSubscriptionOrThrowAsync(Organization organization)
    {
        try
        {
            return await stripeAdapter.GetSubscriptionAsync(organization.GatewaySubscriptionId,
                new SubscriptionGetOptions { Expand = ["items.data.price"] });
        }
        catch (StripeException stripeException) when (stripeException.StripeError?.Code == ErrorCodes.ResourceMissing)
        {
            logger.LogError("Subscription ({SubscriptionId}) for organization ({OrganizationId}) was not found",
                organization.GatewaySubscriptionId, organization.Id);
            throw new NotFoundException();
        }
    }

    /// <summary>
    /// Builds the preview options for an organization that has no live subscription (e.g. Free) and is purchasing a new plan.
    /// </summary>
    /// <param name="organization">The organization for which to build the purchase options.</param>
    /// <param name="newPlan">The new plan that the organization is purchasing.</param>
    /// <returns>The preview options for the purchase.</returns>
    private Task<InvoiceCreatePreviewOptions> BuildPurchaseOptionsAsync(Organization organization, Plan newPlan)
    {
        var items = new List<InvoiceSubscriptionDetailsItemOptions>
        {
            new()
            {
                Price = newPlan.HasNonSeatBasedPasswordManagerPlan()
                    ? newPlan.PasswordManager.StripePlanId
                    : newPlan.PasswordManager.StripeSeatPlanId,
                Quantity = newPlan.HasNonSeatBasedPasswordManagerPlan() ? 1 : organization.Seats ?? 0
            }
        };

        if (organization.UseSecretsManager && newPlan.SecretsManager != null)
        {
            items.Add(new InvoiceSubscriptionDetailsItemOptions
            {
                Price = newPlan.SecretsManager.StripeSeatPlanId, Quantity = organization.SmSeats ?? 0
            });
        }

        return Task.FromResult(new InvoiceCreatePreviewOptions
        {
            Customer = organization.GatewayCustomerId,
            SubscriptionDetails = new InvoiceSubscriptionDetailsOptions { Items = items }
        });
    }

    /// <summary>
    /// Builds the set of subscription changes required to transition an organization from its current plan to a new plan.
    /// </summary>
    /// <param name="organization">The organization for which to build the subscription change set.</param>
    /// <param name="currentPlan">The organization's current plan.</param>
    /// <param name="newPlan">The new plan to which the organization is transitioning.</param>
    /// <returns>The set of subscription changes required to transition the organization from its current plan to the new plan.</returns>
    private static OrganizationSubscriptionChangeSet BuildPlanChangeSet(Organization organization, Plan currentPlan,
        Plan newPlan)
    {
        var builder = OrganizationSubscriptionChangeSet.Builder(currentPlan);

        var isPackagedWithAdditionalSeatPrice = !string.IsNullOrEmpty(currentPlan.PasswordManager.StripePlanId) &&
                                                !string.IsNullOrEmpty(currentPlan.PasswordManager.StripeSeatPlanId);
        builder = isPackagedWithAdditionalSeatPrice
            ? builder.ChangePackagedPasswordManagerPrice(newPlan, organization.Seats ?? 0)
            : builder.ChangePasswordManagerPrice(newPlan);

        if (organization.MaxStorageGb > currentPlan.PasswordManager.BaseStorageGb)
        {
            builder.ChangeStoragePrice(newPlan);
        }

        if (organization.UseSecretsManager)
        {
            builder.ChangeSecretsManagerSeatPrice(newPlan);

            if (organization.SmServiceAccounts > currentPlan.SecretsManager.BaseServiceAccount)
            {
                builder.ChangeServiceAccountPrice(newPlan);
            }
        }

        return builder.Build();
    }

    // Resolves a change against the live subscription (matching by price to its item Id) into a preview line item.
    private InvoiceSubscriptionDetailsItemOptions ToPreviewItem(
        OrganizationSubscriptionChange change,
        IReadOnlyDictionary<string, SubscriptionItem> itemsByPriceId,
        Organization organization) =>
        change.Match(
            add => new InvoiceSubscriptionDetailsItemOptions { Price = add.PriceId, Quantity = add.Quantity },
            changePrice =>
            {
                var item = ResolveItem(changePrice.CurrentPriceId, itemsByPriceId, organization);
                return new InvoiceSubscriptionDetailsItemOptions
                {
                    Id = item.Id,
                    Price = changePrice.UpdatedPriceId,
                    Quantity = changePrice.Quantity ?? item.Quantity
                };
            },
            remove =>
            {
                var item = ResolveItem(remove.PriceId, itemsByPriceId, organization);
                return new InvoiceSubscriptionDetailsItemOptions { Id = item.Id, Deleted = true };
            },
            updateQuantity =>
            {
                var item = ResolveItem(updateQuantity.PriceId, itemsByPriceId, organization);
                return new InvoiceSubscriptionDetailsItemOptions { Id = item.Id, Quantity = updateQuantity.Quantity };
            });

    private SubscriptionItem ResolveItem(
        string priceId, IReadOnlyDictionary<string, SubscriptionItem> itemsByPriceId, Organization organization)
    {
        if (itemsByPriceId.TryGetValue(priceId, out var item))
        {
            return item;
        }

        logger.LogError(
            "Organization {OrganizationId}'s subscription {SubscriptionId} has no line item matching price {PriceId}",
            organization.Id, organization.GatewaySubscriptionId, priceId);

        throw new BadRequestException(
            "Your organization's subscription does not match its current plan. Please contact support for assistance.");
    }
}
