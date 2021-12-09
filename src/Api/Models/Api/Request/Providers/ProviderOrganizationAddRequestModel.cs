using System;
using System.ComponentModel.DataAnnotations;

namespace Bit.Web.Models.Api
{
    public class ProviderOrganizationAddRequestModel
    {
        [Required]
        public Guid OrganizationId { get; set; }

        [Required]
        public string Key { get; set; }
    }
}
