using System.ComponentModel.DataAnnotations;
using Bit.Core.KeyManagement.Models.Api.Request;

namespace Bit.Api.Models.Request.Organizations;

public class OrganizationUserResetPasswordRequestModel
{
    [StringLength(300)]
    public string? NewMasterPasswordHash { get; set; }
    public string? Key { get; set; }

    public MasterPasswordAuthenticationDataRequestModel? MasterPasswordAuthentication { get; set; }
    public MasterPasswordUnlockDataRequestModel? MasterPasswordUnlock { get; set; }

    public bool UnlockAndAuthenticationDataExist()
    {
        return MasterPasswordAuthentication is not null && MasterPasswordUnlock is not null;
    }
}
