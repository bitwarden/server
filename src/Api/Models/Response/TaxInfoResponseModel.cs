using Bit.Core.Models.Business;

namespace Bit.Api.Models.Response;

public class TaxInfoResponseModel
{
    public TaxInfoResponseModel() { }

    public TaxInfoResponseModel(TaxInfo taxInfo)
    {
        if (taxInfo == null)
        {
            return;
        }

        TaxIdNumber = taxInfo.TaxIdNumber;
        TaxIdType = taxInfo.TaxIdType;
        Line1 = taxInfo.BillingAddressLine1;
        Line2 = taxInfo.BillingAddressLine2;
        City = taxInfo.BillingAddressCity;
        State = taxInfo.BillingAddressState;
        PostalCode = taxInfo.BillingAddressPostalCode;
        Country = taxInfo.BillingAddressCountry;
    }

    public string TaxIdNumber { get; set; }
    public string TaxIdType { get; set; }
    public string Line1 { get; set; }
    public string Line2 { get; set; }
    public string City { get; set; }
    public string State { get; set; }
    public string PostalCode { get; set; }
    public string Country { get; set; }
}
