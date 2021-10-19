using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Models.Api.Request.Accounts
{
    public class UpdateTempPasswordRequestModel : OrganizationUserResetPasswordRequestModel
    {
        [StringLength(50)]
        public string MasterPasswordHint { get; set; }
    }
}
