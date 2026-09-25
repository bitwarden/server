using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Models;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Billing.Tax.Services;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Invoicing.InvoicePreviews;
using Bit.Invoicing.InvoicePreviews.Models;
using Bit.Subscriptions.Organization.Models.Requests;
using Microsoft.Extensions.Logging;
using Stripe;
using OrganizationPlan = Bit.Core.Models.StaticStore.Plan;
using UserEntity = Bit.Core.Entities.User;

namespace Bit.Subscriptions.Organization.Queries;

internal interface IPreviewOrganizationSubscriptionPurchaseQuery
{
    Task<InvoicePreview> Run(UserEntity user, PreviewOrganizationSubscriptionPurchaseRequest request);
}

internal sealed class PreviewOrganizationSubscriptionPurchaseQuery(
    ILogger<PreviewOrganizationSubscriptionPurchaseQuery> logger,
    IPricingClient pricingClient,
    ISubscriptionDiscountService subscriptionDiscountService,
    ITaxService taxService,
    IInvoicePreviewService invoicePreviewService) : IPreviewOrganizationSubscriptionPurchaseQuery
{
    private const string CatalogFaultMessage = "The plan could not be previewed. Please contact support for assistance.";

    public async Task<InvoicePreview> Run(UserEntity user, PreviewOrganizationSubscriptionPurchaseRequest request)
    {
        var (purchase, tier, cadence, passwordManager) = ValidatePurchase(request.Purchase);
        var customerDetails = ResolveBillingAddress(request.BillingAddress);
        var (planType, planTier) = ResolvePlan(tier, cadence);

        var options = BuildBaseOptions(customerDetails);

        if (passwordManager.Sponsored)
        {
            var items = new List<InvoiceSubscriptionDetailsItemOptions>
            {
                new()
                {
                    Price = SponsoredPlans.Get(PlanSponsorshipType.FamiliesForEnterprise).StripePlanId,
                    Quantity = 1
                }
            };

            // Redemption swaps the Families package for the sponsored price but keeps the storage add-on, so
            // storage still bills (and is taxed) at the Families storage price.
            if (passwordManager.AdditionalStorage > 0)
            {
                var familiesPlan = await GetPlanAsync(user, planType);
                items.Add(new InvoiceSubscriptionDetailsItemOptions
                {
                    Price = familiesPlan.PasswordManager.StripeStoragePlanId,
                    Quantity = passwordManager.AdditionalStorage
                });
            }

            options.SubscriptionDetails.Items = items;
        }
        else
        {
            var plan = await GetPlanAsync(user, planType);

            options.SubscriptionDetails.Items = BuildItems(plan, passwordManager, purchase.SecretsManager);

            if (purchase.SecretsManager is { Standalone: true })
            {
                options.Discounts = [new InvoiceDiscountOptions { Coupon = StripeConstants.CouponIDs.SecretsManagerStandalone }];
            }
            else if (tier == ProductTierType.Families)
            {
                // Only Families supports coupons; Teams and Enterprise ignore them.
                options.Discounts = await ResolveEligibleFamiliesDiscountsAsync(user, purchase.Coupons);
            }
        }

        InvoicePreview preview;
        try
        {
            preview = await invoicePreviewService.GetInvoicePreviewAsync(options, planTier, cadence);
        }
        catch (StripeException stripeException)
            when (stripeException.StripeError?.Code == StripeConstants.ErrorCodes.CustomerTaxLocationInvalid)
        {
            throw new BadRequestException(
                "Your location wasn't recognized. Please ensure your country and postal code are valid and try again.");
        }
        catch (StripeException stripeException)
            when (stripeException.StripeError?.Code == StripeConstants.ErrorCodes.TaxIdInvalid)
        {
            throw new BadRequestException(
                "The tax ID number you provided was invalid. Please try again or contact support for assistance.");
        }

        return RequireSeats(preview, user, options.SubscriptionDetails.Items);
    }

    private static (PurchaseSelections Purchase, ProductTierType Tier, PlanCadenceType Cadence, PasswordManagerSelections PasswordManager)
        ValidatePurchase(PurchaseSelections? purchase)
    {
        if (purchase is null)
        {
            throw new BadRequestException(
                nameof(PreviewOrganizationSubscriptionPurchaseRequest.Purchase),
                "The Purchase field is required.");
        }

        if (purchase.PasswordManager is not { } passwordManager)
        {
            throw new BadRequestException(
                $"{nameof(PreviewOrganizationSubscriptionPurchaseRequest.Purchase)}.{nameof(PurchaseSelections.PasswordManager)}",
                "The PasswordManager field is required.");
        }

        if (purchase.Tier is not { } tier)
        {
            throw new BadRequestException(
                $"{nameof(PreviewOrganizationSubscriptionPurchaseRequest.Purchase)}.{nameof(PurchaseSelections.Tier)}",
                "The Tier field is required.");
        }

        if (tier is not (ProductTierType.Families or ProductTierType.Teams or ProductTierType.Enterprise))
        {
            throw new BadRequestException(
                $"{nameof(PreviewOrganizationSubscriptionPurchaseRequest.Purchase)}.{nameof(PurchaseSelections.Tier)}",
                $"Cannot purchase the {tier} plan.");
        }

        if (purchase.Cadence is not { } cadence)
        {
            throw new BadRequestException(
                $"{nameof(PreviewOrganizationSubscriptionPurchaseRequest.Purchase)}.{nameof(PurchaseSelections.Cadence)}",
                "The Cadence field is required.");
        }

        if (!Enum.IsDefined(cadence))
        {
            throw new BadRequestException(
                $"{nameof(PreviewOrganizationSubscriptionPurchaseRequest.Purchase)}.{nameof(PurchaseSelections.Cadence)}",
                $"Cadence {cadence} is not supported.");
        }

        if (tier == ProductTierType.Families)
        {
            if (cadence == PlanCadenceType.Monthly)
            {
                throw new BadRequestException(
                    $"{nameof(PreviewOrganizationSubscriptionPurchaseRequest.Purchase)}.{nameof(PurchaseSelections.Cadence)}",
                    "Monthly cadence is not available on the Families plan.");
            }

            if (purchase.SecretsManager != null)
            {
                throw new BadRequestException(
                    $"{nameof(PreviewOrganizationSubscriptionPurchaseRequest.Purchase)}.{nameof(PurchaseSelections.SecretsManager)}",
                    "Secrets Manager is not available on the Families plan.");
            }
        }
        else if (passwordManager.Sponsored)
        {
            throw new BadRequestException(
                $"{nameof(PreviewOrganizationSubscriptionPurchaseRequest.Purchase)}.{nameof(PurchaseSelections.PasswordManager)}.{nameof(PasswordManagerSelections.Sponsored)}",
                "Sponsorship is only available on the Families plan.");
        }

        if (passwordManager.Seats is < 1 or > 100000)
        {
            throw new BadRequestException(
                $"{nameof(PreviewOrganizationSubscriptionPurchaseRequest.Purchase)}.{nameof(PurchaseSelections.PasswordManager)}.{nameof(PasswordManagerSelections.Seats)}",
                "Password Manager seats must be between 1 and 100,000.");
        }

        if (passwordManager.AdditionalStorage is < 0 or > 99)
        {
            throw new BadRequestException(
                $"{nameof(PreviewOrganizationSubscriptionPurchaseRequest.Purchase)}.{nameof(PurchaseSelections.PasswordManager)}.{nameof(PasswordManagerSelections.AdditionalStorage)}",
                "Additional storage must be between 0 and 99 GB.");
        }

        if (purchase.SecretsManager is { } secretsManager)
        {
            if (secretsManager.Seats is < 1 or > 100000)
            {
                throw new BadRequestException(
                    $"{nameof(PreviewOrganizationSubscriptionPurchaseRequest.Purchase)}.{nameof(PurchaseSelections.SecretsManager)}.{nameof(SecretsManagerSelections.Seats)}",
                    "Secrets Manager seats must be between 1 and 100,000.");
            }

            if (secretsManager.AdditionalServiceAccounts is < 0 or > 100000)
            {
                throw new BadRequestException(
                    $"{nameof(PreviewOrganizationSubscriptionPurchaseRequest.Purchase)}.{nameof(PurchaseSelections.SecretsManager)}.{nameof(SecretsManagerSelections.AdditionalServiceAccounts)}",
                    "Additional service accounts must be between 0 and 100,000.");
            }
        }

        return (purchase, tier, cadence, passwordManager);
    }

    private InvoiceCustomerDetailsOptions ResolveBillingAddress(BillingAddressSelections? billingAddress)
    {
        if (billingAddress is null)
        {
            throw new BadRequestException(
                nameof(PreviewOrganizationSubscriptionPurchaseRequest.BillingAddress),
                "The BillingAddress field is required.");
        }

        if (string.IsNullOrWhiteSpace(billingAddress.Country) || billingAddress.Country.Length != 2)
        {
            throw new BadRequestException(
                $"{nameof(PreviewOrganizationSubscriptionPurchaseRequest.BillingAddress)}.{nameof(BillingAddressSelections.Country)}",
                "Country code must be 2 characters long.");
        }

        if (string.IsNullOrWhiteSpace(billingAddress.PostalCode))
        {
            throw new BadRequestException(
                $"{nameof(PreviewOrganizationSubscriptionPurchaseRequest.BillingAddress)}.{nameof(BillingAddressSelections.PostalCode)}",
                "The PostalCode field is required.");
        }

        var customerDetails = new InvoiceCustomerDetailsOptions
        {
            Address = new AddressOptions { Country = billingAddress.Country, PostalCode = billingAddress.PostalCode }
        };

        if (billingAddress.TaxId is not { } taxId)
        {
            return customerDetails;
        }

        if (string.IsNullOrWhiteSpace(taxId.Code))
        {
            throw new BadRequestException(
                $"{nameof(PreviewOrganizationSubscriptionPurchaseRequest.BillingAddress)}.{nameof(BillingAddressSelections.TaxId)}.{nameof(TaxIdSelection.Code)}",
                "The Code field is required.");
        }

        if (string.IsNullOrWhiteSpace(taxId.Value))
        {
            throw new BadRequestException(
                $"{nameof(PreviewOrganizationSubscriptionPurchaseRequest.BillingAddress)}.{nameof(BillingAddressSelections.TaxId)}.{nameof(TaxIdSelection.Value)}",
                "The Value field is required.");
        }

        var derivedCode = taxService.GetStripeTaxCode(billingAddress.Country, taxId.Value);
        if (derivedCode == null)
        {
            // Never log the tax ID value itself; it can be personal data.
            logger.LogWarning(
                "Could not derive Stripe tax ID type for country {Country}; falling back to client-supplied type {TaxIdType}",
                billingAddress.Country, taxId.Code);
        }

        var taxIdType = derivedCode ?? taxId.Code;
        customerDetails.TaxIds =
        [
            new InvoiceCustomerDetailsTaxIdOptions { Type = taxIdType, Value = taxId.Value }
        ];

        if (taxIdType == StripeConstants.TaxIdType.SpanishNIF)
        {
            customerDetails.TaxIds.Add(new InvoiceCustomerDetailsTaxIdOptions
            {
                Type = StripeConstants.TaxIdType.EUVAT,
                Value = $"ES{taxId.Value}"
            });
        }

        return customerDetails;
    }

    private static (PlanType PlanType, PlanTierType PlanTier) ResolvePlan(ProductTierType tier, PlanCadenceType cadence) =>
        (tier, cadence) switch
        {
            (ProductTierType.Families, _) => (PlanType.FamiliesAnnually, PlanTierType.Families),
            (ProductTierType.Teams, PlanCadenceType.Annually) => (PlanType.TeamsAnnually, PlanTierType.Teams),
            (ProductTierType.Teams, PlanCadenceType.Monthly) => (PlanType.TeamsMonthly, PlanTierType.Teams),
            (ProductTierType.Enterprise, PlanCadenceType.Annually) => (PlanType.EnterpriseAnnually, PlanTierType.Enterprise),
            (ProductTierType.Enterprise, PlanCadenceType.Monthly) => (PlanType.EnterpriseMonthly, PlanTierType.Enterprise),
            _ => throw new InvalidOperationException($"No plan maps to {tier} {cadence}.")
        };

    private static InvoiceCreatePreviewOptions BuildBaseOptions(InvoiceCustomerDetailsOptions customerDetails) => new()
    {
        AutomaticTax = new InvoiceAutomaticTaxOptions { Enabled = true },
        Currency = "usd",
        CustomerDetails = customerDetails,
        SubscriptionDetails = new InvoiceSubscriptionDetailsOptions
        {
            BillingMode = new InvoiceSubscriptionDetailsBillingModeOptions { Type = StripeConstants.BillingMode.Classic }
        }
    };

    private async Task<OrganizationPlan> GetPlanAsync(UserEntity user, PlanType planType)
    {
        var plan = await pricingClient.GetPlan(planType);
        if (plan is null)
        {
            logger.LogError(
                "Organization purchase preview for user ({UserId}) found no {PlanType} plan in the pricing service",
                user.Id, planType);
            throw new ConflictException(CatalogFaultMessage);
        }

        return plan;
    }

    private static List<InvoiceSubscriptionDetailsItemOptions> BuildItems(
        OrganizationPlan plan, PasswordManagerSelections passwordManager, SecretsManagerSelections? secretsManager)
    {
        // Packaged plans (Families) bill one package regardless of seats, matching subscription creation.
        var items = new List<InvoiceSubscriptionDetailsItemOptions>
        {
            plan.HasNonSeatBasedPasswordManagerPlan()
                ? new() { Price = plan.PasswordManager.StripePlanId, Quantity = 1 }
                : new() { Price = plan.PasswordManager.StripeSeatPlanId, Quantity = passwordManager.Seats }
        };

        if (passwordManager.AdditionalStorage > 0)
        {
            items.Add(new InvoiceSubscriptionDetailsItemOptions
            {
                Price = plan.PasswordManager.StripeStoragePlanId,
                Quantity = passwordManager.AdditionalStorage
            });
        }

        if (secretsManager is { Seats: > 0 })
        {
            items.Add(new InvoiceSubscriptionDetailsItemOptions
            {
                Price = plan.SecretsManager.StripeSeatPlanId,
                Quantity = secretsManager.Seats
            });

            if (secretsManager.AdditionalServiceAccounts > 0)
            {
                items.Add(new InvoiceSubscriptionDetailsItemOptions
                {
                    Price = plan.SecretsManager.StripeServiceAccountPlanId,
                    Quantity = secretsManager.AdditionalServiceAccounts
                });
            }
        }

        return items;
    }

    // All-or-nothing: an ineligible coupon drops every coupon rather than failing the preview.
    private async Task<List<InvoiceDiscountOptions>?> ResolveEligibleFamiliesDiscountsAsync(UserEntity user, string[]? coupons)
    {
        var trimmedCoupons = (coupons ?? [])
            .Where(coupon => !string.IsNullOrWhiteSpace(coupon))
            .Select(coupon => coupon.Trim())
            .ToArray();

        if (trimmedCoupons.Length == 0)
        {
            return null;
        }

        var allEligible = await subscriptionDiscountService.ValidateDiscountEligibilityForUserAsync(
            user, trimmedCoupons, DiscountTierType.Families);

        return allEligible
            ? trimmedCoupons.Select(coupon => new InvoiceDiscountOptions { Coupon = coupon }).ToList()
            : null;
    }

    private InvoicePreview RequireSeats(InvoicePreview preview, UserEntity user, IEnumerable<InvoiceSubscriptionDetailsItemOptions> items)
    {
        if (preview.PasswordManager.Seats is not null)
        {
            return preview;
        }

        logger.LogError(
            "Organization purchase preview for user ({UserId}) resolved no Password Manager seats line. Prices={PriceIds}",
            user.Id, string.Join(",", items.Select(item => item.Price)));
        throw new ConflictException(CatalogFaultMessage);
    }
}
