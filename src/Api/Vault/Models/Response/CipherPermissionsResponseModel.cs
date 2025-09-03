// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

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
            throw new Exception("OrganizationAbility not found for organization cipher.");
        }

        Delete = NormalCipherPermissions.CanDelete(user, cipherDetails, organizationAbility);
        Restore = NormalCipherPermissions.CanRestore(user, cipherDetails, organizationAbility);
    }
}
