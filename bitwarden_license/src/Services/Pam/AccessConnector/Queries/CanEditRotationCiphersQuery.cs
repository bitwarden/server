using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Vault.Queries;
using Bit.Services.Pam.AccessConnector.Queries.Interfaces;

namespace Bit.Services.Pam.AccessConnector.Queries;

/// <inheritdoc cref="ICanEditRotationCiphersQuery" />
public class CanEditRotationCiphersQuery : ICanEditRotationCiphersQuery
{
    private readonly ICurrentContext _currentContext;
    private readonly IOrganizationAbilityCacheService _organizationAbilityCacheService;
    private readonly IGetCipherPermissionsForUserQuery _getCipherPermissionsForUserQuery;

    public CanEditRotationCiphersQuery(
        ICurrentContext currentContext,
        IOrganizationAbilityCacheService organizationAbilityCacheService,
        IGetCipherPermissionsForUserQuery getCipherPermissionsForUserQuery)
    {
        _currentContext = currentContext;
        _organizationAbilityCacheService = organizationAbilityCacheService;
        _getCipherPermissionsForUserQuery = getCipherPermissionsForUserQuery;
    }

    public async Task<bool> CanEditAllAsync(Guid organizationId, IEnumerable<Guid> cipherIds)
    {
        var ids = cipherIds.ToList();
        if (ids.Count == 0)
        {
            return true;
        }

        if (await CanEditEveryCipherAsync(organizationId))
        {
            return true;
        }

        var permissions = await _getCipherPermissionsForUserQuery.GetByOrganization(organizationId);
        return ids.All(id => permissions.TryGetValue(id, out var p) && p.Edit && p.ViewPassword);
    }

    /// <summary>
    /// Whether <see cref="IGetCipherPermissionsForUserQuery.GetByOrganization"/> grants Edit and ViewPassword on every
    /// cipher, so the read cannot change the answer. Mirrors its <c>CanEditAllCiphersAsync</c>; the read returns no
    /// ciphers for a disabled organization.
    /// </summary>
    private async Task<bool> CanEditEveryCipherAsync(Guid organizationId)
    {
        var org = _currentContext.GetOrganization(organizationId);
        if (org is null || !_currentContext.UserId.HasValue)
        {
            return false;
        }

        var orgAbility = await _organizationAbilityCacheService.GetOrganizationAbilityAsync(org.Id);
        if (orgAbility is not { Enabled: true })
        {
            return false;
        }

        return org is { Type: OrganizationUserType.Custom, Permissions.EditAnyCollection: true }
               || (orgAbility.AllowAdminAccessToAllCollectionItems
                   && org is { Type: OrganizationUserType.Admin or OrganizationUserType.Owner });
    }
}
