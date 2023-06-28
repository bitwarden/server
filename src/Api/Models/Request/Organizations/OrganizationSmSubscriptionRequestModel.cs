using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Models.Request.Organizations;

public class OrganizationSmSubscriptionRequestModel
{
    [Required]
    [Range(0, int.MaxValue)]
    public int AdditionalSeats { get; set; }
    [Range(0, int.MaxValue)]
    public int? AdditionalServiceAccounts { get; set; }
}
