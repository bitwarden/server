// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Models.Request.Organizations;

public class OrganizationUserResetPasswordRequestModel
{
    [Required]
    [StringLength(300)]
    public string NewMasterPasswordHash { get; set; }
    [Required]
    public string Key { get; set; }
}
