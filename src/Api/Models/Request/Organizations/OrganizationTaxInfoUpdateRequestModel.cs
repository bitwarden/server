using Bit.Api.Models.Request.Accounts;

namespace Bit.Api.Models.Request.Organizations;

public class OrganizationTaxInfoUpdateRequestModel : TaxInfoUpdateRequestModel
{
    public string TaxId { get; set; }
    public string Line1 { get; set; }
    public string Line2 { get; set; }
    public string City { get; set; }
    public string State { get; set; }
}
