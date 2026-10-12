using Bit.Core.AdminConsole.OrganizationFeatures.ScopedOrganizationApiKeys.Interfaces;
using Bit.Core.Auth.IdentityServer;
using Bit.Core.Repositories;
using Bit.Core.SecretsManager.Repositories;

namespace Bit.Core.AdminConsole.OrganizationFeatures.ScopedOrganizationApiKeys;

public class GetScopedOrganizationApiKeyClientQuery(
    IApiKeyRepository apiKeyRepository,
    IOrganizationRepository organizationRepository,
    TimeProvider timeProvider)
    : IGetScopedOrganizationApiKeyClientQuery
{
    public async Task<ScopedOrganizationApiKeyClient?> GetAsync(Guid organizationId, Guid keyId)
    {
        var key = await apiKeyRepository.GetByIdAsync(keyId);
        if (key is null ||
            key.ServiceAccountId is not null ||
            key.OrganizationId != organizationId ||
            key.ExpireAt <= timeProvider.GetUtcNow().UtcDateTime)
        {
            return null;
        }

        var organization = await organizationRepository.GetByIdAsync(organizationId);
        if (organization is null || !organization.Enabled || !organization.UseApi)
        {
            return null;
        }

        return new ScopedOrganizationApiKeyClient(
            organization.Id,
            key.Id,
            key.ClientSecretHash,
            key.ExpireAt,
            (key.GetScopes() ?? []).Intersect(ApiScopes.OrganizationApiKeyScopes).ToList());
    }
}
