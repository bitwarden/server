using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Models
{
    public class SecurityStampRequestModel
    {
        [Required]
        public string MasterPasswordHash { get; set; }
    }
}
