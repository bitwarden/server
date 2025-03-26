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

    /// <summary>
    /// (optional) The user to target for the sponsorship.
    /// </summary>
    /// <remarks>Left empty when creating a sponsorship for the authenticated user.</remarks>
    public Guid? SponsoringUserId { get; set; }

    public string Notes { get; set; }
}
