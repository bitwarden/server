using Bit.Core.Billing.Constants;
using Stripe;

namespace Bit.Core.Billing.Extensions;

public static class CustomerExtensions
{
    public static bool HasBillingLocation(this Customer customer)
        => customer is
        {
            Address:
            {
                Country: not null and not "",
                PostalCode: not null and not ""
            }
        };

    public static bool HasRecognizedTaxLocation(this Customer customer) =>
        customer?.Tax?.AutomaticTax != StripeConstants.AutomaticTaxStatus.UnrecognizedLocation;

    public static decimal GetBillingBalance(this Customer customer)
    {
        return customer != null ? customer.Balance / 100M : default;
    }

    public static bool ApprovedToPayByInvoice(this Customer customer)
        => customer.Metadata != null &&
           customer.Metadata.TryGetValue(StripeConstants.MetadataKeys.InvoiceApproved, out var value) &&
           int.TryParse(value, out var invoiceApproved) && invoiceApproved == 1;

    public static string GetRegion(this Customer customer)
    {
        const string defaultRegion = Core.Constants.CountryAbbreviations.UnitedStates;

        if (customer.Metadata == null)
        {
            return defaultRegion;
        }

        if (customer.Metadata.TryGetValue(StripeConstants.MetadataKeys.Region, out var value))
        {
            return value;
        }

        var incorrectlyCasedRegionKey = customer.Metadata.Keys
            .FirstOrDefault(key => key.Equals(StripeConstants.MetadataKeys.Region, StringComparison.OrdinalIgnoreCase));

        if (incorrectlyCasedRegionKey is null)
        {
            return defaultRegion;
        }

        _ = customer.Metadata.TryGetValue(incorrectlyCasedRegionKey, out var regionValue);

        return !string.IsNullOrWhiteSpace(regionValue)
            ? regionValue
            : defaultRegion;
    }
}
