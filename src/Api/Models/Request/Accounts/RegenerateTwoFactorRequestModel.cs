using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Models
{
    public class RegenerateTwoFactorRequestModel
    {
        [Required]
        public string MasterPasswordHash { get; set; }
        [Required]
        [StringLength(50)]
        public string Token { get; set; }
    }
}
