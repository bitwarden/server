using Bit.Core.Entities;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Vault.Authorization.Permissions;
using Bit.Core.Vault.Models.Data;

namespace Bit.Api.Vault.Models.Response;

public record CipherPermissionsResponseModel
{
    public bool Delete { get; init; }
    public bool Restore { get; init; }

    public CipherPermissionsResponseModel(
        User user,
        CipherDetails cipherDetails,
        OrganizationAbility organizationAbility)
    {
        Delete = NormalCipherPermissions.CanDelete(user, cipherDetails, organizationAbility);
        Restore = NormalCipherPermissions.CanRestore(user, cipherDetails, organizationAbility);
    }
}
