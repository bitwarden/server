using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Models
{
    public class EmailTokenRequestModel
    {
        [Required]
        [EmailAddress]
        public string NewEmail { get; set; }
        [Required]
        public string MasterPasswordHash { get; set; }
    }
}
