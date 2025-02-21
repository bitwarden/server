using Bit.Core.Entities;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Vault.Authorization.Permissions;
using Bit.Core.Vault.Models.Data;

public class CipherPermissionsResponseModel
{
    public bool Delete { get; set; }
    public bool Restore { get; set; }

    public CipherPermissionsResponseModel(
        User user,
        CipherDetails cipherDetails,
        OrganizationAbility organizationAbility)
    {
        Delete = NormalCipherPermissions.CanDelete(user, cipherDetails, organizationAbility);
        Restore = NormalCipherPermissions.CanRestore(user, cipherDetails, organizationAbility);
    }
}
