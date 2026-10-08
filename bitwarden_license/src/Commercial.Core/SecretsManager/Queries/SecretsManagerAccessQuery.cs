using Bit.Core.Context;
using Bit.Core.Repositories;
using Bit.Core.SecretsManager.Queries.Interfaces;

namespace Bit.Commercial.Core.SecretsManager.Queries;

public class SecretsManagerAccessQuery : ISecretsManagerAccessQuery
{
    private readonly ICurrentContext _currentContext;
    private readonly IOrganizationRepository _organizationRepository;

    // Scoped per request, so an organization is only read once even when a request authorizes several resources.
    private readonly Dictionary<Guid, bool> _organizationAccess = new();

    public SecretsManagerAccessQuery(ICurrentContext currentContext, IOrganizationRepository organizationRepository)
    {
        _currentContext = currentContext;
        _organizationRepository = organizationRepository;
    }

    public async Task<bool> HasAccessAsync(Guid organizationId)
    {
        if (!_currentContext.AccessSecretsManager(organizationId))
        {
            return false;
        }

        if (_organizationAccess.TryGetValue(organizationId, out var hasAccess))
        {
            return hasAccess;
        }

        var organization = await _organizationRepository.GetByIdAsync(organizationId);
        hasAccess = organization is { Enabled: true, UseSecretsManager: true };
        _organizationAccess[organizationId] = hasAccess;
        return hasAccess;
    }
}
