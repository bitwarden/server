using Bit.Core.Models.Business;

namespace Bit.Core.Billing.Models;

public record TaxInformation(
    string Country,
    string PostalCode,
    string TaxId,
    string TaxIdType,
    string Line1,
    string Line2,
    string City,
    string State)
{
    public static TaxInformation From(TaxInfo taxInfo) => new(
        taxInfo.BillingAddressCountry,
        taxInfo.BillingAddressPostalCode,
        taxInfo.TaxIdNumber,
        taxInfo.TaxIdType,
        taxInfo.BillingAddressLine1,
        taxInfo.BillingAddressLine2,
        taxInfo.BillingAddressCity,
        taxInfo.BillingAddressState);
}
