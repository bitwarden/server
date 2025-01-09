namespace Bit.Core.Models.Business;

public class TaxInfo
{
    public string TaxIdNumber { get; set; }
    public string TaxIdType { get; set; }

    public string StripeTaxRateId { get; set; }
    public string BillingAddressLine1 { get; set; }
    public string BillingAddressLine2 { get; set; }
    public string BillingAddressCity { get; set; }
    public string BillingAddressState { get; set; }
    public string BillingAddressPostalCode { get; set; }
    public string BillingAddressCountry { get; set; } = "US";

    public bool HasTaxId
    {
        get => !string.IsNullOrWhiteSpace(TaxIdNumber) &&
            !string.IsNullOrWhiteSpace(TaxIdType);
    }
}
