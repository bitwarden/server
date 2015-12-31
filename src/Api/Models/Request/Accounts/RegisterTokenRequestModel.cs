using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Models
{
    public class RegisterTokenRequestModel
    {
        [Required]
        [EmailAddress]
        [StringLength(50)]
        public string Email { get; set; }
    }
}
