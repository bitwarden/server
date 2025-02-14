
using Bit.Core.Entities;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Vault.Models.Data;

namespace Bit.Core.Vault.Authorization.Permissions;

public class NormalCipherPermissions
{
    public static bool CanDelete(User user, CipherDetails cipherDetails, OrganizationAbility organizationAbility)
    {
        if (user.Id == cipherDetails.UserId)
        {
            return true;
        }

        if (organizationAbility.LimitItemDeletion)
        {
            return cipherDetails.Manage;
        }
        return cipherDetails.Manage || cipherDetails.Edit;
    }

    public static bool CanRestore(User user, CipherDetails cipherDetails, OrganizationAbility organizationAbility)
    {
        return CanDelete(user, cipherDetails, organizationAbility);
    }
}
