using System.ComponentModel.DataAnnotations;
using Bit.Core.Enums;

namespace Bit.Api.Models.Request.Organizations;

public class OrganizationSponsorshipRedeemRequestModel
{
    [Required]
    public PlanSponsorshipType PlanSponsorshipType { get; set; }

    [Required]
    public Guid SponsoredOrganizationId { get; set; }
}
