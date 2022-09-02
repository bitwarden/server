namespace Bit.Admin.Models;

public class TaxRateAddEditModel
{
    public string StripeTaxRateId { get; set; }
    public string Country { get; set; }
    public string State { get; set; }
    public string PostalCode { get; set; }
    public decimal Rate { get; set; }
}
