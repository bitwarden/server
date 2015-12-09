using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Models
{
    public class PasswordHintRequestModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }
    }
}
