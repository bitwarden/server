using System;
using System.ComponentModel.DataAnnotations;
using Bit.Core.Utilities;

namespace Bit.Core.Models.Api.Request
{
    public class OrganizationSponsorshipRequestModel
    {
        [Required]
        public Guid OrganizationUserId { get; set; }
        [Required]
        [StringLength(256)]
        [StrictEmailAddress]
        public string sponsoredEmail { get; set; }
    }
}
