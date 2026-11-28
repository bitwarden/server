using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.Context;
using Bit.Core.SecretsManager.Queries.Interfaces;

namespace Bit.Commercial.Core.SecretsManager.Queries;

public class SecretsManagerAccessQuery : ISecretsManagerAccessQuery
{
    private readonly ICurrentContext _currentContext;
    private readonly IOrganizationAbilityCacheService _organizationAbilityCacheService;

    public SecretsManagerAccessQuery(ICurrentContext currentContext,
        IOrganizationAbilityCacheService organizationAbilityCacheService)
    {
        _currentContext = currentContext;
        _organizationAbilityCacheService = organizationAbilityCacheService;
    }

    public async Task<bool> HasAccessAsync(Guid organizationId)
    {
        if (!_currentContext.AccessSecretsManager(organizationId))
        {
            return false;
        }

        var ability = await _organizationAbilityCacheService.GetOrganizationAbilityAsync(organizationId);
        return ability is { Enabled: true, UseSecretsManager: true };
    }
}
