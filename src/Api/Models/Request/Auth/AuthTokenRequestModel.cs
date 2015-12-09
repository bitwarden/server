using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Models
{
    public class AuthTokenRequestModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }
        [Required]
        public string MasterPasswordHash { get; set; }
    }
}
