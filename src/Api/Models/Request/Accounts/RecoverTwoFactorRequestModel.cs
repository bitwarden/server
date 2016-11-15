using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Models
{
    public class RecoverTwoFactorRequestModel
    {
        [Required]
        [EmailAddress]
        [StringLength(50)]
        public string Email { get; set; }
        [Required]
        public string MasterPasswordHash { get; set; }
        [Required]
        [StringLength(32)]
        public string RecoveryCode { get; set; }
    }
}
