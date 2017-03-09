using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Models.Api
{
    public class AuthTokenRequestModel
    {
        [Required]
        [EmailAddress]
        [StringLength(50)]
        public string Email { get; set; }
        [Required]
        public string MasterPasswordHash { get; set; }
        public DeviceRequestModel Device { get; set; }
    }
}
