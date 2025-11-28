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
        => customer.Metadata.TryGetValue(StripeConstants.MetadataKeys.InvoiceApproved, out var value) &&
           int.TryParse(value, out var invoiceApproved) && invoiceApproved == 1;
}
