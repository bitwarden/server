using System;
using System.ComponentModel.DataAnnotations;
using Bit.Core.Enums;

namespace Bit.Core.Models.Api
{
    public class OrganizationSponsorshipRedeemRequestModel
    {
        [Required]
        public PlanSponsorshipType PlanSponsorshipType { get; set; }
        [Required]
        public Guid SponsoredOrganizationId { get; set; }
    }
}
