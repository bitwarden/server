using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Models
{
    public class AuthTokenTwoFactorRequestModel
    {
        [Required]
        public string Code { get; set; }
        [Required]
        public string Provider { get; set; }
    }
}
