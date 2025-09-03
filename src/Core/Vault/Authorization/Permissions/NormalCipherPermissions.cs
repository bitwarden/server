#nullable enable
using Bit.Core.Entities;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Vault.Models.Data;

namespace Bit.Core.Vault.Authorization.Permissions;

public class NormalCipherPermissions
{
    public static bool CanDelete(User user, CipherDetails cipherDetails, OrganizationAbility? organizationAbility)
    {
        if (cipherDetails.OrganizationId == null && cipherDetails.UserId == null)
        {
            throw new Exception("Cipher needs to belong to a user or an organization.");
        }

        if (user.Id == cipherDetails.UserId)
        {
            return true;
        }

        if (organizationAbility?.Id != cipherDetails.OrganizationId)
        {
            throw new Exception("Cipher does not belong to the input organization.");
        }

        if (organizationAbility is { LimitItemDeletion: true })
        {
            return cipherDetails.Manage;
        }
        return cipherDetails.Manage || cipherDetails.Edit;
    }

    public static bool CanRestore(User user, CipherDetails cipherDetails, OrganizationAbility? organizationAbility)
    {
        return CanDelete(user, cipherDetails, organizationAbility);
    }
}
