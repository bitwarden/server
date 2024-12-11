using System.ComponentModel.DataAnnotations;
using Bit.Core.Billing.Models;

namespace Bit.Api.Billing.Models.Requests;

public class TaxInformationRequestBody
{
    [Required]
    public string Country { get; set; }

    [Required]
    public string PostalCode { get; set; }
    public string TaxId { get; set; }
    public string Line1 { get; set; }
    public string Line2 { get; set; }
    public string City { get; set; }
    public string State { get; set; }

    public TaxInformation ToDomain() => new(Country, PostalCode, TaxId, Line1, Line2, City, State);
}
