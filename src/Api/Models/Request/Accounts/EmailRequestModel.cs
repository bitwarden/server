using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Models
{
    public class EmailRequestModel
    {
        [Required]
        [EmailAddress]
        public string NewEmail { get; set; }
        [Required]
        public string MasterPasswordHash { get; set; }
        [Required]
        public string NewMasterPasswordHash { get; set; }
        [Required]
        public string Token { get; set; }
        [Required]
        public CipherRequestModel[] Ciphers { get; set; }
    }
}
