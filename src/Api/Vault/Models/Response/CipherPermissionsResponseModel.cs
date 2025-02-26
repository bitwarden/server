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
        IDictionary<Guid, OrganizationAbility> organizationAbilities)
    {
        OrganizationAbility organizationAbility = null;
        if (cipherDetails.OrganizationId.HasValue && !organizationAbilities.TryGetValue(cipherDetails.OrganizationId.Value, out organizationAbility))
        {
            throw new Exception("Organization-owned cipher missing required organization abilities in the provided dictionary.");
        }

        Delete = NormalCipherPermissions.CanDelete(user, cipherDetails, organizationAbility);
        Restore = NormalCipherPermissions.CanRestore(user, cipherDetails, organizationAbility);
    }
}
