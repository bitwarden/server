using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Models
{
    public class EmailRequestModel
    {
        [Required]
        [EmailAddress]
        [StringLength(50)]
        public string NewEmail { get; set; }
        [Required]
        [StringLength(300)]
        public string MasterPasswordHash { get; set; }
        [Required]
        [StringLength(300)]
        public string NewMasterPasswordHash { get; set; }
        [Required]
        public string Token { get; set; }
        [Required]
        public CipherRequestModel[] Ciphers { get; set; }
    }
}
