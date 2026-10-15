using Bit.Core.Vault.Queries;
using Bit.Services.Pam.AccessConnector.Queries.Interfaces;

namespace Bit.Services.Pam.AccessConnector.Queries;

/// <inheritdoc cref="ICanEditRotationCiphersQuery" />
public class CanEditRotationCiphersQuery : ICanEditRotationCiphersQuery
{
    private readonly IGetCipherPermissionsForUserQuery _getCipherPermissionsForUserQuery;

    public CanEditRotationCiphersQuery(IGetCipherPermissionsForUserQuery getCipherPermissionsForUserQuery)
    {
        _getCipherPermissionsForUserQuery = getCipherPermissionsForUserQuery;
    }

    public async Task<bool> CanEditAllAsync(Guid organizationId, IEnumerable<Guid> cipherIds)
    {
        var ids = cipherIds.ToList();
        if (ids.Count == 0)
        {
            return true;
        }

        var permissions = await _getCipherPermissionsForUserQuery.GetByOrganization(organizationId);
        return ids.All(id => permissions.TryGetValue(id, out var p) && p.Edit && p.ViewPassword);
    }
}
