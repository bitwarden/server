using Bit.Core.Billing.Commands;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Tax.Services;
using Bit.Core.Services;
using Microsoft.Extensions.Logging;
using Stripe;

namespace Bit.Core.Billing.Tax.Commands;

using static StripeConstants;

public interface IPreviewTaxAmountCommand
{
    Task<BillingCommandResult<decimal>> Run(OrganizationTrialParameters parameters);
}

public class PreviewTaxAmountCommand(
    ILogger<PreviewTaxAmountCommand> logger,
    IPricingClient pricingClient,
    IStripeAdapter stripeAdapter,
    ITaxService taxService) : BaseBillingCommand<PreviewTaxAmountCommand>(logger), IPreviewTaxAmountCommand
{
    protected override Conflict DefaultConflict
        => new("We had a problem calculating your tax obligation. Please contact support for assistance.");

    public Task<BillingCommandResult<decimal>> Run(OrganizationTrialParameters parameters)
        => HandleAsync<decimal>(async () =>
        {
            var (planType, productType, taxInformation) = parameters;

            var plan = await pricingClient.GetPlanOrThrow(planType);

            var options = new InvoiceCreatePreviewOptions
            {
                Currency = "usd",
                CustomerDetails = new InvoiceCustomerDetailsOptions
                {
                    Address = new AddressOptions
                    {
                        Country = taxInformation.Country,
                        PostalCode = taxInformation.PostalCode
                    }
                },
                SubscriptionDetails = new InvoiceSubscriptionDetailsOptions
                {
                    Items =
                    [
                        new InvoiceSubscriptionDetailsItemOptions
                        {
                            Price = plan.HasNonSeatBasedPasswordManagerPlan()
                                ? plan.PasswordManager.StripePlanId
                                : plan.PasswordManager.StripeSeatPlanId,
                            Quantity = 1
                        }
                    ]
                }
            };

            if (productType == ProductType.SecretsManager)
            {
                options.SubscriptionDetails.Items.Add(new InvoiceSubscriptionDetailsItemOptions
                {
                    Price = plan.SecretsManager.StripeSeatPlanId,
                    Quantity = 1
                });

                options.Discounts =
                [
                    new InvoiceDiscountOptions { Coupon = CouponIDs.SecretsManagerStandalone }
                ];
            }

            if (!string.IsNullOrEmpty(taxInformation.TaxId))
            {
                var taxIdType = taxService.GetStripeTaxCode(
                    taxInformation.Country,
                    taxInformation.TaxId);

                if (string.IsNullOrEmpty(taxIdType))
                {
                    return new BadRequest(
                        "We couldn't find a corresponding tax ID type for the tax ID you provided. Please try again or contact support for assistance.");
                }

                options.CustomerDetails.TaxIds =
                [
                    new InvoiceCustomerDetailsTaxIdOptions { Type = taxIdType, Value = taxInformation.TaxId }
                ];

                if (taxIdType == TaxIdType.SpanishNIF)
                {
                    options.CustomerDetails.TaxIds.Add(new InvoiceCustomerDetailsTaxIdOptions
                    {
                        Type = TaxIdType.EUVAT,
                        Value = $"ES{parameters.TaxInformation.TaxId}"
                    });
                }
            }

            options.AutomaticTax = new InvoiceAutomaticTaxOptions { Enabled = true };
            if (parameters.PlanType.IsBusinessProductTierType() &&
                parameters.TaxInformation.Country != Core.Constants.CountryAbbreviations.UnitedStates)
            {
                options.CustomerDetails.TaxExempt = StripeConstants.TaxExempt.Reverse;
            }

            var invoice = await stripeAdapter.InvoiceCreatePreviewAsync(options);
            return Convert.ToDecimal(invoice.TotalTaxes.Sum(invoiceTotalTax => invoiceTotalTax.Amount)) / 100;
        });
}

#region Command Parameters

public record OrganizationTrialParameters
{
    public required PlanType PlanType { get; set; }
    public required ProductType ProductType { get; set; }
    public required TaxInformationDTO TaxInformation { get; set; }

    public void Deconstruct(
        out PlanType planType,
        out ProductType productType,
        out TaxInformationDTO taxInformation)
    {
        planType = PlanType;
        productType = ProductType;
        taxInformation = TaxInformation;
    }

    public record TaxInformationDTO
    {
        public required string Country { get; set; }
        public required string PostalCode { get; set; }
        public string? TaxId { get; set; }
    }
}

#endregion
