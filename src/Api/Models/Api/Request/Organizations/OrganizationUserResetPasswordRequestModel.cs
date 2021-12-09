using System.ComponentModel.DataAnnotations;

namespace Bit.Web.Models.Api
{
    public class OrganizationUserResetPasswordRequestModel
    {
        [Required]
        [StringLength(300)]
        public string NewMasterPasswordHash { get; set; }
        [Required]
        public string Key { get; set; }
    }
}
