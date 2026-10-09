// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Auth.Identity;
using Bit.Core.Auth.IdentityServer;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Duende.IdentityModel;
using Duende.IdentityServer.Models;

namespace Bit.Identity.IdentityServer.ClientProviders;

internal class OrganizationClientProvider : IClientProvider
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IOrganizationApiKeyRepository _organizationApiKeyRepository;
    private readonly IOrganizationScopedApiKeyRepository _organizationScopedApiKeyRepository;
    private readonly Bitwarden.Server.Sdk.Features.IFeatureService _featureService;

    public OrganizationClientProvider(
        IOrganizationRepository organizationRepository,
        IOrganizationApiKeyRepository organizationApiKeyRepository,
        IOrganizationScopedApiKeyRepository organizationScopedApiKeyRepository,
        Bitwarden.Server.Sdk.Features.IFeatureService featureService
    )
    {
        _organizationRepository = organizationRepository;
        _organizationApiKeyRepository = organizationApiKeyRepository;
        _organizationScopedApiKeyRepository = organizationScopedApiKeyRepository;
        _featureService = featureService;
    }

    public async Task<Client> GetAsync(string identifier)
    {
        if (identifier.Contains('.'))
        {
            return await GetScopedAsync(identifier);
        }

        if (!Guid.TryParse(identifier, out var organizationId))
        {
            return null;
        }

        var organization = await _organizationRepository.GetByIdAsync(organizationId);

        if (organization == null)
        {
            return null;
        }

        var orgApiKey = (await _organizationApiKeyRepository
            .GetManyByOrganizationIdTypeAsync(organization.Id, OrganizationApiKeyType.Default))
            .First();

        return new Client
        {
            ClientId = $"organization.{organization.Id}",
            RequireClientSecret = true,
            ClientSecrets = [new Secret(orgApiKey.ApiKey.Sha256())],
            AllowedScopes = [ApiScopes.ApiOrganization],
            AllowedGrantTypes = GrantTypes.ClientCredentials,
            AccessTokenLifetime = 3600 * 1,
            Enabled = organization.Enabled && organization.UseApi,
            Claims =
            [
                new(JwtClaimTypes.Subject, organization.Id.ToString()),
                new(Claims.Type, IdentityClientType.Organization.ToString())
            ],
        };
    }

    /// <param name="identifier">An identifier of the form <c>{organizationId}.{keyId}</c>.</param>
    private async Task<Client> GetScopedAsync(string identifier)
    {
        var segments = identifier.Split('.');
        if (segments.Length != 2 ||
            !Guid.TryParseExact(segments[0], "D", out var organizationId) ||
            !Guid.TryParseExact(segments[1], "D", out var keyId))
        {
            return null;
        }

        if (!_featureService.IsEnabled(FeatureFlagKeys.ScopedOrganizationApiKeys))
        {
            return null;
        }

        var key = await _organizationScopedApiKeyRepository.GetByIdAsync(keyId);
        if (key == null || key.OrganizationId != organizationId || key.ExpireAt <= DateTime.UtcNow)
        {
            return null;
        }

        var organization = await _organizationRepository.GetByIdAsync(organizationId);
        if (organization == null || !organization.Enabled || !organization.UseApi)
        {
            return null;
        }

        return new Client
        {
            ClientId = $"organization.{organization.Id}.{key.Id}",
            RequireClientSecret = true,
            ClientSecrets = [new Secret(key.ClientSecretHash)],
            AllowedScopes = (key.GetScopes() ?? []).Intersect(ApiScopes.OrganizationApiKeyScopes).ToList(),
            AllowedGrantTypes = GrantTypes.ClientCredentials,
            AccessTokenLifetime = 3600 * 1,
            Claims =
            [
                new(JwtClaimTypes.Subject, organization.Id.ToString()),
                new(Claims.Type, IdentityClientType.Organization.ToString())
            ],
        };
    }
}
