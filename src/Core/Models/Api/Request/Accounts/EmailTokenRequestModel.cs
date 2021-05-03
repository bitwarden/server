using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Models.Api
{
    public class EmailTokenRequestModel
    {
        [Required]
        [EmailAddress]
        [StringLength(256)]
        public string NewEmail { get; set; }
        [Required]
        [StringLength(300)]
        public string MasterPasswordHash { get; set; }
    }
}
