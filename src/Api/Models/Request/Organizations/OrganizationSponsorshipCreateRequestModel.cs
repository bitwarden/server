using System.ComponentModel.DataAnnotations;
using Bit.Core.Enums;
using Bit.Core.Utilities;

namespace Bit.Api.Models.Request.Organizations;

public class OrganizationSponsorshipCreateRequestModel
{
    [Required]
    public PlanSponsorshipType PlanSponsorshipType { get; set; }

    [Required]
    [StringLength(256)]
    [StrictEmailAddress]
    public string SponsoredEmail { get; set; }

    [StringLength(256)]
    public string FriendlyName { get; set; }
}
