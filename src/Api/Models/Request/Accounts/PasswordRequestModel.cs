using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Models.Request.Accounts
{
    public class PasswordRequestModel : SecretVerificationRequestModel
    {
        [Required]
        [StringLength(300)]
        public string NewMasterPasswordHash { get; set; }
        [Required]
        public string Key { get; set; }
    }
}
