using System;
using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Models.Api
{
    public class VerifyPasswordRequestModel
    {
        [Required]
        [StringLength(300)]
        public string MasterPasswordHash { get; set; }
    }
}
