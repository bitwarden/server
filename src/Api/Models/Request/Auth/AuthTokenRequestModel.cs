using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Models
{
    public class AuthTokenRequestModel
    {
        [Required]
        [EmailAddress]
        [StringLength(50)]
        public string Email { get; set; }
        [Required]
        public string MasterPasswordHash { get; set; }
    }
}
