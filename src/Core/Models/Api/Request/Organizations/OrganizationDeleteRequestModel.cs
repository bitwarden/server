using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Models.Api
{
    public class OrganizationDeleteRequestModel
    {
        [Required]
        public string MasterPasswordHash { get; set; }
    }
}
