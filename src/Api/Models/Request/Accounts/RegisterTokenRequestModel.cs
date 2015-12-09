using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Models
{
    public class RegisterTokenRequestModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }
    }
}
