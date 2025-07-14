// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.ComponentModel.DataAnnotations;
using Bit.Api.Models.Request.Organizations;

namespace Bit.Api.Auth.Models.Request.Accounts;

public class UpdateTempPasswordRequestModel : OrganizationUserResetPasswordRequestModel
{
    [StringLength(50)]
    public string MasterPasswordHint { get; set; }
}
