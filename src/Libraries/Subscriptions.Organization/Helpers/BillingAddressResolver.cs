using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Payment.Models;
using Bit.Core.Billing.Tax.Services;
using Bit.Core.Exceptions;
using Bit.Subscriptions.Organization.Models.Requests;
using Microsoft.Extensions.Logging;
using Stripe;

namespace Bit.Subscriptions.Organization.Helpers;

using static StripeConstants;

internal static class BillingAddressResolver
{
    private const string BillingAddressKey = "BillingAddress";

    public static AddressOptions ResolveAddress(BillingAddressSelections? billingAddress)
    {
        if (billingAddress is null)
        {
            throw new BadRequestException(BillingAddressKey, "The BillingAddress field is required.");
        }

        if (string.IsNullOrWhiteSpace(billingAddress.Country) || billingAddress.Country.Length != 2)
        {
            throw new BadRequestException(
                $"{BillingAddressKey}.{nameof(BillingAddressSelections.Country)}",
                "Country code must be 2 characters long.");
        }

        if (string.IsNullOrWhiteSpace(billingAddress.PostalCode))
        {
            throw new BadRequestException(
                $"{BillingAddressKey}.{nameof(BillingAddressSelections.PostalCode)}",
                "The PostalCode field is required.");
        }

        return new AddressOptions { Country = billingAddress.Country, PostalCode = billingAddress.PostalCode };
    }

    public static TaxID? ValidateTaxId(TaxIdSelection? taxId)
    {
        if (taxId is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(taxId.Code))
        {
            throw new BadRequestException(
                $"{BillingAddressKey}.{nameof(BillingAddressSelections.TaxId)}.{nameof(TaxIdSelection.Code)}",
                "The Code field is required.");
        }

        if (string.IsNullOrWhiteSpace(taxId.Value))
        {
            throw new BadRequestException(
                $"{BillingAddressKey}.{nameof(BillingAddressSelections.TaxId)}.{nameof(TaxIdSelection.Value)}",
                "The Value field is required.");
        }

        return new TaxID(taxId.Code, taxId.Value);
    }

    public static List<InvoiceCustomerDetailsTaxIdOptions>? ResolveTaxIds(
        ITaxService taxService, ILogger logger, string country, TaxID? taxId)
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
}
