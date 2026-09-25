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
using Bit.Subscriptions.User.Models.Requests;
using Microsoft.Extensions.Logging;
using Stripe;
using static Bit.Subscriptions.User.Models.Requests.GetOrganizationPurchasePreviewRequest;
using OrganizationPlan = Bit.Core.Models.StaticStore.Plan;
using UserEntity = Bit.Core.Entities.User;

namespace Bit.Subscriptions.User.Queries;

internal interface IGetOrganizationPurchasePreviewQuery
{
    Task<InvoicePreview> Run(UserEntity user, GetOrganizationPurchasePreviewRequest request);
}

internal sealed class GetOrganizationPurchasePreviewQuery(
    ILogger<GetOrganizationPurchasePreviewQuery> logger,
    IPricingClient pricingClient,
    ISubscriptionDiscountService subscriptionDiscountService,
    ITaxService taxService,
    IInvoicePreviewService invoicePreviewService) : IGetOrganizationPurchasePreviewQuery
{
    public async Task<InvoicePreview> Run(UserEntity user, GetOrganizationPurchasePreviewRequest request)
    {
        var (purchase, passwordManager, billingAddress) = Validate(request);
        var (planType, planTier) = ResolvePlan(purchase.Tier, purchase.Cadence);

        var options = BuildBaseOptions(billingAddress);

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
                // The system coupon takes precedence; user coupons are ignored for standalone Secrets Manager.
                options.Discounts = [new InvoiceDiscountOptions { Coupon = StripeConstants.CouponIDs.SecretsManagerStandalone }];
            }
            else if (purchase.Tier == ProductTierType.Families)
            {
                options.Discounts = await ResolveEligibleFamiliesDiscountsAsync(user, purchase.Coupons);
            }
        }

        InvoicePreview preview;
        try
        {
            preview = await invoicePreviewService.GetInvoicePreviewAsync(options, planTier, purchase.Cadence);
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

        return PurchasePreviewGuard.RequireSeats(
            preview, logger, user.Id, options.SubscriptionDetails.Items.Select(item => item.Price));
    }

    private static (PurchaseSelections Purchase, PasswordManagerSelections PasswordManager, BillingAddressSelections BillingAddress)
        Validate(GetOrganizationPurchasePreviewRequest request)
    {
        var purchase = request.Purchase
            ?? throw new BadRequestException("Purchase", "The Purchase field is required.");
        var passwordManager = purchase.PasswordManager
            ?? throw new BadRequestException("Purchase.PasswordManager", "The PasswordManager field is required.");

        if (purchase.Tier is not (ProductTierType.Families or ProductTierType.Teams or ProductTierType.Enterprise))
        {
            throw new BadRequestException("Purchase.Tier", $"Cannot purchase the {purchase.Tier} plan.");
        }

        if (!Enum.IsDefined(purchase.Cadence))
        {
            throw new BadRequestException("Purchase.Cadence", $"Cadence {purchase.Cadence} is not supported.");
        }

        if (purchase.Tier == ProductTierType.Families)
        {
            if (purchase.Cadence == PlanCadenceType.Monthly)
            {
                throw new BadRequestException("Purchase.Cadence", "Monthly cadence is not available on the Families plan.");
            }

            if (purchase.SecretsManager != null)
            {
                throw new BadRequestException("Purchase.SecretsManager", "Secrets Manager is not available on the Families plan.");
            }
        }
        else if (passwordManager.Sponsored)
        {
            throw new BadRequestException("Purchase.PasswordManager.Sponsored", "Sponsorship is only available on the Families plan.");
        }

        if (passwordManager.Seats is < 1 or > 100000)
        {
            throw new BadRequestException("Purchase.PasswordManager.Seats", "Password Manager seats must be between 1 and 100,000");
        }

        if (passwordManager.AdditionalStorage is < 0 or > 99)
        {
            throw new BadRequestException("Purchase.PasswordManager.AdditionalStorage", "Additional storage must be between 0 and 99 GB");
        }

        if (purchase.SecretsManager is { } secretsManager)
        {
            if (secretsManager.Seats is < 1 or > 100000)
            {
                throw new BadRequestException("Purchase.SecretsManager.Seats", "Secrets Manager seats must be between 1 and 100,000");
            }

            if (secretsManager.AdditionalServiceAccounts is < 0 or > 100000)
            {
                throw new BadRequestException("Purchase.SecretsManager.AdditionalServiceAccounts",
                    "Additional service accounts must be between 0 and 100,000");
            }
        }

        var billingAddress = request.BillingAddress
            ?? throw new BadRequestException("BillingAddress", "The BillingAddress field is required.");

        if (string.IsNullOrWhiteSpace(billingAddress.Country) || billingAddress.Country.Length != 2)
        {
            throw new BadRequestException("BillingAddress.Country", "Country code must be 2 characters long.");
        }

        if (string.IsNullOrWhiteSpace(billingAddress.PostalCode))
        {
            throw new BadRequestException("BillingAddress.PostalCode", "The PostalCode field is required.");
        }

        if (billingAddress.TaxId is { } taxId)
        {
            if (string.IsNullOrWhiteSpace(taxId.Code))
            {
                throw new BadRequestException("BillingAddress.TaxId.Code", "The Code field is required.");
            }

            if (string.IsNullOrWhiteSpace(taxId.Value))
            {
                throw new BadRequestException("BillingAddress.TaxId.Value", "The Value field is required.");
            }
        }

        return (purchase, passwordManager, billingAddress);
    }

    private async Task<OrganizationPlan> GetPlanAsync(UserEntity user, PlanType planType)
    {
        var plan = await pricingClient.GetPlan(planType);
        if (plan is null)
        {
            logger.LogError(
                "Organization purchase preview for user ({UserId}) found no {PlanType} plan in the pricing service",
                user.Id, planType);
            throw new ConflictException(PurchasePreviewGuard.CatalogFaultMessage);
        }

        return plan;
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

    private InvoiceCreatePreviewOptions BuildBaseOptions(BillingAddressSelections billingAddress)
    {
        var options = new InvoiceCreatePreviewOptions
        {
            AutomaticTax = new InvoiceAutomaticTaxOptions { Enabled = true },
            Currency = "usd",
            CustomerDetails = new InvoiceCustomerDetailsOptions
            {
                Address = new AddressOptions { Country = billingAddress.Country, PostalCode = billingAddress.PostalCode }
            },
            SubscriptionDetails = new InvoiceSubscriptionDetailsOptions
            {
                BillingMode = new InvoiceSubscriptionDetailsBillingModeOptions { Type = StripeConstants.BillingMode.Classic }
            }
        };

        if (billingAddress.TaxId is not { Code: { } clientCode, Value: { } taxIdValue })
        {
            return options;
        }

        string? derivedCode = taxService.GetStripeTaxCode(billingAddress.Country!, taxIdValue);
        if (derivedCode == null)
        {
            // Never log the tax ID value itself; it can be personal data.
            logger.LogWarning(
                "Could not derive Stripe tax ID type for country {Country}; falling back to client-supplied type {TaxIdType}",
                billingAddress.Country, clientCode);
        }

        var taxIdType = derivedCode ?? clientCode;
        options.CustomerDetails.TaxIds =
        [
            new InvoiceCustomerDetailsTaxIdOptions { Type = taxIdType, Value = taxIdValue }
        ];

        if (taxIdType == StripeConstants.TaxIdType.SpanishNIF)
        {
            options.CustomerDetails.TaxIds.Add(new InvoiceCustomerDetailsTaxIdOptions
            {
                Type = StripeConstants.TaxIdType.EUVAT,
                Value = $"ES{taxIdValue}"
            });
        }

        return options;
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
}
