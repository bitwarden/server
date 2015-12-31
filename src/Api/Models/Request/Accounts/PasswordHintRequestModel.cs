using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Models
{
    public class PasswordHintRequestModel
    {
        [Required]
        [EmailAddress]
        [StringLength(50)]
        public string Email { get; set; }
    }
}
