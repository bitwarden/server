using Bit.Core.Entities;
using Bit.Core.Models.Api;

namespace Bit.Api.Models.Response;

public class TaxRateResponseModel : ResponseModel
{
    public TaxRateResponseModel(TaxRate taxRate)
        : base("profile")
    {
        if (taxRate == null)
        {
            throw new ArgumentNullException(nameof(taxRate));
        }

        Id = taxRate.Id;
        Country = taxRate.Country;
        State = taxRate.State;
        PostalCode = taxRate.PostalCode;
        Rate = taxRate.Rate;
    }

    public string Id { get; set; }
    public string Country { get; set; }
    public string State { get; set; }
    public string PostalCode { get; set; }
    public decimal Rate { get; set; }
}
