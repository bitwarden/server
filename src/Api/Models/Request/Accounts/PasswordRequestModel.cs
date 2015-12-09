using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Models
{
    public class PasswordRequestModel
    {
        [Required]
        public string MasterPasswordHash { get; set; }
        [Required]
        public string NewMasterPasswordHash { get; set; }
        [Required]
        public CipherRequestModel[] Ciphers { get; set; }
    }
}
