using System.ComponentModel.DataAnnotations;
using Bit.Core.AdminConsole.OrganizationFeatures.AccountRecovery.v2;
using Bit.Core.Entities;

namespace Bit.Api.Models.Request.Organizations;

public class OrganizationUserResetPasswordRequestModel
{
    public bool ResetMasterPassword { get; set; }
    public bool ResetTwoFactor { get; set; }

    [StringLength(300)]
    public string? NewMasterPasswordHash { get; set; }
    public string? Key { get; set; }

    public RecoverAccountRequest ToCommandRequest(Guid orgId, OrganizationUser organizationUser) => new()
    {
        OrgId = orgId,
        OrganizationUser = organizationUser,
        ResetMasterPassword = ResetMasterPassword,
        ResetTwoFactor = ResetTwoFactor,
        NewMasterPasswordHash = NewMasterPasswordHash,
        Key = Key
    };
}
