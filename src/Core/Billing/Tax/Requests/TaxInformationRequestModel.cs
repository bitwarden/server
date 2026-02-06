// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Billing.Tax.Requests;

public class TaxInformationRequestModel
{
    [Length(2, 2), Required]
    public string Country { get; set; }

    [Required]
    public string PostalCode { get; set; }

    public string TaxId { get; set; }
}
