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
        public DataReloadRequestModel Data { get; set; }
    }

    [Obsolete]
    public class PasswordRequestModel_Old
    {
        [Required]
        [StringLength(300)]
        public string MasterPasswordHash { get; set; }
        [Required]
        [StringLength(300)]
        public string NewMasterPasswordHash { get; set; }
        [Required]
        public CipherRequestModel[] Ciphers { get; set; }
    }
}
