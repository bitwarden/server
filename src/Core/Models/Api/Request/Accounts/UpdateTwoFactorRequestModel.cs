using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Models.Api
{
    public class UpdateTwoFactorRequestModel
    {
        [Required]
        public string MasterPasswordHash { get; set; }
        [Required]
        public bool? Enabled { get; set; }
        [Required]
        [StringLength(50)]
        public string Token { get; set; }
    }
}
