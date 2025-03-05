#nullable enable
using Bit.Core.Entities;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Models.Data;

namespace Bit.Core.Vault.Authorization.Permissions;

public class NormalCipherPermissions
{
    public static bool CanDelete(User user, Cipher cipher, OrganizationAbility? organizationAbility)
    {
        if (cipher.OrganizationId == null && cipher.UserId == null)
        {
            throw new Exception("Cipher needs to belong to a user or an organization.");
        }

        if (user.Id == cipher.UserId)
        {
            return true;
        }

        if (cipher is not CipherDetails cipherDetails)
        {
            throw new Exception("Cipher is not a CipherDetails.");
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

    public static bool CanRestore(User user, Cipher cipher, OrganizationAbility? organizationAbility)
    {
        return CanDelete(user, cipher, organizationAbility);
    }
}
