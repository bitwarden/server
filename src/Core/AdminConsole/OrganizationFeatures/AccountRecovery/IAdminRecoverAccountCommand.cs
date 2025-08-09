using Bit.Core.Enums;
using Microsoft.AspNetCore.Identity;

namespace Bit.Core.AdminConsole.OrganizationFeatures.AccountRecovery;

public interface IAdminRecoverAccountCommand
{
    Task<IdentityResult> AdminResetPasswordAsync(OrganizationUserType callingUserType, Guid orgId, Guid organizationUserId,
        string newMasterPassword, string key);
}
