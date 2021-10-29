using System;
using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Models.Api
{
    public class OrganizationSponsorshipRedeemRequestModel
    {
        [Required]
        public Guid SponsoredOrganizationId { get; set; }
    }
}
