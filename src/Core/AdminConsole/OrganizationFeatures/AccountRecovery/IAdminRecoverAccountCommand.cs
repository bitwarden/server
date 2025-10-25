using Bit.Core.Entities;
using Microsoft.AspNetCore.Identity;

namespace Bit.Core.AdminConsole.OrganizationFeatures.AccountRecovery;

public interface IAdminRecoverAccountCommand
{
    Task<IdentityResult> RecoverAccountAsync(Guid orgId, OrganizationUser organizationUser,
        string newMasterPassword, string key);
}
