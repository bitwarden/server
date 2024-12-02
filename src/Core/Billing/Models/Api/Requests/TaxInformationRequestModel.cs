using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Billing.Models.Api.Requests;

public class TaxInformationRequestModel
{
    [Length(2, 2), Required]
    public string Country { get; set; }

    [Required]
    public string PostalCode { get; set; }

    public string TaxId { get; set; }
}
