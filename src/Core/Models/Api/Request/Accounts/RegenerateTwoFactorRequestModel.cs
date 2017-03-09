using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Models.Api
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
