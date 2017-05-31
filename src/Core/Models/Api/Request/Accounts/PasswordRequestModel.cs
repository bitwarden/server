using System;
using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Models.Api
{
    public class PasswordRequestModel
    {
        [Required]
        [StringLength(300)]
        public string MasterPasswordHash { get; set; }
        [Required]
        [StringLength(300)]
        public string NewMasterPasswordHash { get; set; }
        [Required]
        public string Key { get; set; }
    }
}
