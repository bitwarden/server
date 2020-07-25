using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Models.Api.Request.Accounts
{
    public class PasswordNoCompareRequestModel
    {
        [Required]
        [StringLength(300)]
        public string NewMasterPasswordHash { get; set; }
        [Required]
        public string Key { get; set; }
    }
}
