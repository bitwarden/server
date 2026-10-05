using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Billing.Payment.Models;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Billing.Tax.Services;
using Bit.Core.Exceptions;
using Bit.Invoicing.InvoicePreviews;
using Bit.Invoicing.InvoicePreviews.Models;
using Bit.Subscriptions.Organization.Models.Requests;
using Microsoft.Extensions.Logging;
using Stripe;

namespace Bit.Subscriptions.Organization.Commands;

using static StripeConstants;
using OrganizationEntity = Core.AdminConsole.Entities.Organization;
using Plan = Core.Models.StaticStore.Plan;

internal interface IPreviewOrganizationPlanChangeCommand
{
    /**
     * <summary>
     * Previews the invoice for an organization's plan change request.
     * </summary>
     * <param name="organization">The organization requesting the plan change.</param>
     * <param name="request">The plan change request containing the desired tier, cadence, and billing address.</param>
     * <returns>An <see cref="InvoicePreview"/> representing the preview of the invoice for the plan change.</returns>
     *
     * <exception cref="BadRequestException">Thrown when the request is invalid, such as missing billing address or unsupported plan.</exception>
     * <exception cref="ConflictException">Thrown when the organization's subscription is in an invalid state.</exception>
     */
    Task<InvoicePreview> Run(OrganizationEntity organization, PreviewOrganizationPlanChangeRequest request);
}

internal sealed class PreviewOrganizationPlanChangeCommand(
    ILogger<PreviewOrganizationPlanChangeCommand> logger,
    IPricingClient pricingClient,
    IStripeAdapter stripeAdapter,
    ITaxService taxService,
    IInvoicePreviewService invoicePreviewService) : IPreviewOrganizationPlanChangeCommand
{
    private const string InvalidSubscriptionMessage =
        "Your organization's subscription is in an invalid state. Please contact support for assistance.";

    private static readonly string[] _previewableSubscriptionStatuses =
        [SubscriptionStatus.Trialing, SubscriptionStatus.Active, SubscriptionStatus.PastDue];

    public async Task<InvoicePreview> Run(OrganizationEntity organization, PreviewOrganizationPlanChangeRequest request)
    {
        var tier = request.Tier;
        var cadence = request.Cadence;
        var address = ResolveBillingAddress(request.BillingAddress);
        var requestTaxId = ValidateRequestTaxId(request.BillingAddress!.TaxId);
        var newPlan = await pricingClient.GetPlanOrThrow(ResolvePlanType(tier, cadence));

        if (organization.UseSecretsManager && !newPlan.SupportsSecretsManager)
        {
            throw new BadRequestException("The selected plan does not support Secrets Manager.");
        }

        var subscriptionId = string.IsNullOrEmpty(organization.GatewaySubscriptionId)
            ? null
            : organization.GatewaySubscriptionId;

        InvoiceSubscriptionDetailsOptions subscriptionDetails;
        TaxID? onFileTaxId = null;
        if (subscriptionId is not null)
        {
            (subscriptionDetails, onFileTaxId) = await BuildPlanChangeDetailsAsync(organization, newPlan);
        }
        else if (organization.PlanType == PlanType.Free)
        {
            subscriptionDetails = BuildPurchaseDetails(organization, newPlan);
        }
        else
        {
            logger.LogError(
                "Organization ({OrganizationId}) has no subscription to preview a plan change for", organization.Id);
            throw new ConflictException(InvalidSubscriptionMessage);
        }

        var options = new InvoiceCreatePreviewOptions
        {
            Customer = organization.GatewayCustomerId,
            Subscription = subscriptionId,
            SubscriptionDetails = subscriptionDetails,
            AutomaticTax = new InvoiceAutomaticTaxOptions { Enabled = true },
            CustomerDetails = new InvoiceCustomerDetailsOptions
            {
                Address = address,
                TaxIds = ResolveTaxIds(request.BillingAddress!.Country!, requestTaxId ?? onFileTaxId)
            }
        };

        // Stripe sources currency and billing mode from the existing subscription on a plan change; a purchase
        // preview has none, so set them explicitly. billing_mode can't be sent alongside a subscription.
        if (subscriptionId is null)
        {
            options.Currency = "usd";
            options.SubscriptionDetails.BillingMode =
                new InvoiceSubscriptionDetailsBillingModeOptions { Type = BillingMode.Classic };
        }

        try
        {
            return await invoicePreviewService.GetInvoicePreviewAsync(options, tier, cadence);
        }
        catch (StripeException stripeException)
            when (stripeException.StripeError?.Code == ErrorCodes.CustomerTaxLocationInvalid)
        {
            throw new BadRequestException(
                "Your location wasn't recognized. Please ensure your country and postal code are valid and try again.");
        }
        catch (StripeException stripeException)
            when (stripeException.StripeError?.Code == ErrorCodes.TaxIdInvalid)
        {
            throw new BadRequestException(
                "Your tax ID wasn't recognized for your selected country. Please ensure your country and tax ID are valid.");
        }
    }

    private async Task<(InvoiceSubscriptionDetailsOptions SubscriptionDetails, TaxID? OnFileTaxId)>
        BuildPlanChangeDetailsAsync(OrganizationEntity organization, Plan newPlan)
    {
        var currentPlan = await pricingClient.GetPlanOrThrow(organization.PlanType);

        if (currentPlan.UpgradeSortOrder == newPlan.UpgradeSortOrder)
        {
            throw new BadRequestException("Your organization is already on this plan.");
        }

        if (currentPlan.UpgradeSortOrder > newPlan.UpgradeSortOrder)
        {
            throw new BadRequestException("You can't downgrade your organization's plan.");
        }

        var subscription = await GetSubscriptionOrThrowAsync(organization);

        if (!_previewableSubscriptionStatuses.Contains(subscription.Status))
        {
            logger.LogError(
                "Organization ({OrganizationId}) subscription ({SubscriptionId}) has status ({Status}) that cannot be previewed",
                organization.Id, subscription.Id, subscription.Status);
            throw new ConflictException(InvalidSubscriptionMessage);
        }

        var itemsByPriceId = subscription.Items.ToDictionary(item => item.Price.Id);

        var changeSet = BuildPlanChangeSet(organization, currentPlan, newPlan);
        var items = changeSet.Changes
            .Select(change => ToPreviewItem(change, itemsByPriceId, organization))
            .ToList();

        var subscriptionDetails = new InvoiceSubscriptionDetailsOptions
        {
            Items = items,
            ProrationBehavior = ProrationBehavior.AlwaysInvoice
        };

        // A trialing subscription isn't charged for the change now (the proration is $0). End the trial in the
        // preview so the amount reflects what the subscriber pays when the trial converts.
        if (subscription.Status == SubscriptionStatus.Trialing)
        {
            subscriptionDetails.TrialEnd = InvoiceSubscriptionDetailsTrialEnd.Now;
        }

        return (subscriptionDetails, ExtractOnFileTaxId(subscription.Customer));
    }

    private InvoiceSubscriptionDetailsOptions BuildPurchaseDetails(OrganizationEntity organization, Plan newPlan)
    {
        var items = new List<InvoiceSubscriptionDetailsItemOptions>();

        if (newPlan.HasNonSeatBasedPasswordManagerPlan())
        {
            items.Add(new InvoiceSubscriptionDetailsItemOptions { Price = newPlan.PasswordManager.StripePlanId, Quantity = 1 });
        }
        else
        {
            if (organization.Seats is not { } seats)
            {
                logger.LogError(
                    "Organization ({OrganizationId}) is missing a Password Manager seat count required to preview its plan change", organization.Id);
                throw new ConflictException(InvalidSubscriptionMessage);
            }

            items.Add(new InvoiceSubscriptionDetailsItemOptions { Price = newPlan.PasswordManager.StripeSeatPlanId, Quantity = seats });
        }

        if (organization.UseSecretsManager && newPlan.SecretsManager != null)
        {
            if (organization.SmSeats is not { } smSeats)
            {
                logger.LogError(
                    "Organization ({OrganizationId}) is missing a Secrets Manager seat count required to preview its plan change", organization.Id);
                throw new ConflictException(InvalidSubscriptionMessage);
            }

            items.Add(new InvoiceSubscriptionDetailsItemOptions { Price = newPlan.SecretsManager.StripeSeatPlanId, Quantity = smSeats });
        }

        // No Subscription is set on this new-subscription preview, so there is nothing to prorate.
        return new InvoiceSubscriptionDetailsOptions { Items = items };
    }

    private OrganizationSubscriptionChangeSet BuildPlanChangeSet(OrganizationEntity organization, Plan currentPlan,
        Plan newPlan)
    {
        var builder = OrganizationSubscriptionChangeSet.Builder(currentPlan);

        var isPackagedWithAdditionalSeatPrice = !string.IsNullOrEmpty(currentPlan.PasswordManager.StripePlanId) &&
                                                !string.IsNullOrEmpty(currentPlan.PasswordManager.StripeSeatPlanId);
        if (isPackagedWithAdditionalSeatPrice)
        {
            if (organization.Seats is not { } seats)
            {
                logger.LogError(
                    "Organization ({OrganizationId}) is missing a Password Manager seat count required to preview its plan change", organization.Id);
                throw new ConflictException(InvalidSubscriptionMessage);
            }

            builder = builder.ChangePackagedPasswordManagerPrice(newPlan, seats);
        }
        else
        {
            builder = builder.ChangePasswordManagerPrice(newPlan);
        }

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

    private InvoiceSubscriptionDetailsItemOptions ToPreviewItem(
        OrganizationSubscriptionChange change,
        IReadOnlyDictionary<string, SubscriptionItem> itemsByPriceId,
        OrganizationEntity organization) =>
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
        string priceId, IReadOnlyDictionary<string, SubscriptionItem> itemsByPriceId, OrganizationEntity organization)
    {
        if (itemsByPriceId.TryGetValue(priceId, out var item))
        {
            return item;
        }

        logger.LogError(
            "Organization {OrganizationId}'s subscription {SubscriptionId} has no line item matching price {PriceId}",
            organization.Id, organization.GatewaySubscriptionId, priceId);

        throw new ConflictException(InvalidSubscriptionMessage);
    }

    private async Task<Subscription> GetSubscriptionOrThrowAsync(OrganizationEntity organization)
    {
        try
        {
            return await stripeAdapter.GetSubscriptionAsync(organization.GatewaySubscriptionId,
                new SubscriptionGetOptions { Expand = ["items.data.price", "customer.tax_ids"] });
        }
        catch (StripeException stripeException) when (stripeException.StripeError?.Code == ErrorCodes.ResourceMissing)
        {
            logger.LogError("Subscription ({SubscriptionId}) for organization ({OrganizationId}) was not found",
                organization.GatewaySubscriptionId, organization.Id);
            throw new ConflictException(InvalidSubscriptionMessage);
        }
    }

    private static AddressOptions ResolveBillingAddress(BillingAddressSelections? billingAddress)
    {
        if (billingAddress is null)
        {
            throw new BadRequestException(
                nameof(PreviewOrganizationPlanChangeRequest.BillingAddress), "The BillingAddress field is required.");
        }

        if (string.IsNullOrWhiteSpace(billingAddress.Country) || billingAddress.Country.Length != 2)
        {
            throw new BadRequestException(
                $"{nameof(PreviewOrganizationPlanChangeRequest.BillingAddress)}.{nameof(BillingAddressSelections.Country)}",
                "Country code must be 2 characters long.");
        }

        if (string.IsNullOrWhiteSpace(billingAddress.PostalCode))
        {
            throw new BadRequestException(
                $"{nameof(PreviewOrganizationPlanChangeRequest.BillingAddress)}.{nameof(BillingAddressSelections.PostalCode)}",
                "The PostalCode field is required.");
        }

        return new AddressOptions { Country = billingAddress.Country, PostalCode = billingAddress.PostalCode };
    }

    private TaxID? ValidateRequestTaxId(TaxIdSelection? requestTaxId)
    {
        if (requestTaxId is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(requestTaxId.Code))
        {
            throw new BadRequestException(
                $"{nameof(PreviewOrganizationPlanChangeRequest.BillingAddress)}.{nameof(BillingAddressSelections.TaxId)}.{nameof(TaxIdSelection.Code)}",
                "The Code field is required.");
        }

        if (string.IsNullOrWhiteSpace(requestTaxId.Value))
        {
            throw new BadRequestException(
                $"{nameof(PreviewOrganizationPlanChangeRequest.BillingAddress)}.{nameof(BillingAddressSelections.TaxId)}.{nameof(TaxIdSelection.Value)}",
                "The Value field is required.");
        }

        return new TaxID(requestTaxId.Code, requestTaxId.Value);
    }

    private List<InvoiceCustomerDetailsTaxIdOptions>? ResolveTaxIds(string country, TaxID? taxId)
    {
        if (taxId is null)
        {
            return null;
        }

        var derivedCode = taxService.GetStripeTaxCode(country, taxId.Value);
        if (derivedCode is null)
        {
            logger.LogWarning(
                "Could not derive Stripe tax ID type for country {Country}; falling back to client-supplied type {TaxIdType}",
                country, taxId.Code);
        }

        var taxIdType = derivedCode ?? taxId.Code;
        var taxIds = new List<InvoiceCustomerDetailsTaxIdOptions>
        {
            new() { Type = taxIdType, Value = taxId.Value }
        };

        // A Spanish NIF also needs an EU VAT entry so Stripe applies the right cross-border rate.
        if (taxIdType == TaxIdType.SpanishNIF)
        {
            taxIds.Add(new InvoiceCustomerDetailsTaxIdOptions { Type = TaxIdType.EUVAT, Value = $"ES{taxId.Value}" });
        }

        return taxIds;
    }

    private static TaxID? ExtractOnFileTaxId(Customer? customer)
    {
        var taxId = customer?.TaxIds?.FirstOrDefault();
        return taxId != null ? new TaxID(taxId.Type, taxId.Value) : null;
    }

    private static PlanType ResolvePlanType(PlanTierType tier, PlanCadenceType cadence) =>
        tier switch
        {
            PlanTierType.Families => cadence == PlanCadenceType.Monthly
                ? throw new BadRequestException("The Families plan is only available on an annual cadence.")
                : PlanType.FamiliesAnnually,
            PlanTierType.Teams => cadence == PlanCadenceType.Monthly
                ? PlanType.TeamsMonthly
                : PlanType.TeamsAnnually,
            PlanTierType.Enterprise => cadence == PlanCadenceType.Monthly
                ? PlanType.EnterpriseMonthly
                : PlanType.EnterpriseAnnually,
            _ => throw new BadRequestException($"Cannot change an organization to the {tier} tier.")
        };
}
