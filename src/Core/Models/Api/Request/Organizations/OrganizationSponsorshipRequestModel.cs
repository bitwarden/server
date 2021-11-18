using System;
using System.ComponentModel.DataAnnotations;
using Bit.Core.Enums;
using Bit.Core.Utilities;

namespace Bit.Core.Models.Api.Request
{
    public class OrganizationSponsorshipRequestModel
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
}
