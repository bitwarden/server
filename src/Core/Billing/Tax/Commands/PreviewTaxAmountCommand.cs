#nullable enable
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Models;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Tax.Services;
using Bit.Core.Services;
using Microsoft.Extensions.Logging;
using Stripe;

namespace Bit.Core.Billing.Tax.Commands;

public interface IPreviewTaxAmountCommand
{
    Task<BillingCommandResult<decimal>> Run(OrganizationTrialParameters parameters);
}

public class PreviewTaxAmountCommand(
    ILogger<PreviewTaxAmountCommand> logger,
    IPricingClient pricingClient,
    IStripeAdapter stripeAdapter,
    ITaxService taxService) : IPreviewTaxAmountCommand
{
    public async Task<BillingCommandResult<decimal>> Run(OrganizationTrialParameters parameters)
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
                Items = [
                    new InvoiceSubscriptionDetailsItemOptions
                    {
                        Price = plan.HasNonSeatBasedPasswordManagerPlan() ? plan.PasswordManager.StripePlanId : plan.PasswordManager.StripeSeatPlanId,
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

            options.Coupon = StripeConstants.CouponIDs.SecretsManagerStandalone;
        }

        if (!string.IsNullOrEmpty(taxInformation.TaxId))
        {
            var taxIdType = taxService.GetStripeTaxCode(
                taxInformation.Country,
                taxInformation.TaxId);

            if (string.IsNullOrEmpty(taxIdType))
            {
                return BadRequest.UnknownTaxIdType;
            }

            options.CustomerDetails.TaxIds = [
                new InvoiceCustomerDetailsTaxIdOptions
                {
                    Type = taxIdType,
                    Value = taxInformation.TaxId
                }
            ];

            if (taxIdType == StripeConstants.TaxIdType.SpanishNIF)
            {
                options.CustomerDetails.TaxIds.Add(new InvoiceCustomerDetailsTaxIdOptions
                {
                    Type = StripeConstants.TaxIdType.EUVAT,
                    Value = $"ES{parameters.TaxInformation.TaxId}"
                });
            }
        }

        if (planType.GetProductTier() == ProductTierType.Families)
        {
            options.AutomaticTax = new InvoiceAutomaticTaxOptions { Enabled = true };
        }
        else
        {
            options.AutomaticTax = new InvoiceAutomaticTaxOptions
            {
                Enabled = options.CustomerDetails.Address.Country == "US" ||
                          options.CustomerDetails.TaxIds is [_, ..]
            };
        }

        try
        {
            var invoice = await stripeAdapter.InvoiceCreatePreviewAsync(options);
            return Convert.ToDecimal(invoice.Tax) / 100;
        }
        catch (StripeException stripeException) when (stripeException.StripeError.Code ==
                                                      StripeConstants.ErrorCodes.CustomerTaxLocationInvalid)
        {
            return BadRequest.TaxLocationInvalid;
        }
        catch (StripeException stripeException) when (stripeException.StripeError.Code ==
                                                      StripeConstants.ErrorCodes.TaxIdInvalid)
        {
            return BadRequest.TaxIdNumberInvalid;
        }
        catch (StripeException stripeException)
        {
            logger.LogError(stripeException, "Stripe responded with an error during {Operation}. Code: {Code}", nameof(PreviewTaxAmountCommand), stripeException.StripeError.Code);
            return new Unhandled();
        }
    }
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
