using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Models.Api
{
    public class PasswordHintRequestModel
    {
        [Required]
        [EmailAddress]
        [StringLength(256)]
        public string Email { get; set; }
    }
}
