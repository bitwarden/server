// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.ComponentModel.DataAnnotations;
using Bit.Core.KeyManagement.Models.Api.Request;

namespace Bit.Api.Models.Request.Organizations;

public class OrganizationUserResetPasswordRequestModel
{
    [StringLength(300)]
    public string NewMasterPasswordHash { get; set; }
    public string Key { get; set; }

    public MasterPasswordAuthenticationDataRequestModel AuthenticationData { get; set; }
    public MasterPasswordUnlockDataRequestModel UnlockData { get; set; }
}
