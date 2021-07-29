using System.ComponentModel.DataAnnotations;
using Bit.Core.Utilities;

namespace Bit.Core.Models.Api.Request
{
    public class ProviderOrganizationCreateRequestModel
    {
        [Required]
        [StrictEmailAddress]
        public string ClientOwnerEmail { get; set; }
        [Required]
        public OrganizationCreateRequestModel OrganizationCreateRequest { get; set; }
    }
}
