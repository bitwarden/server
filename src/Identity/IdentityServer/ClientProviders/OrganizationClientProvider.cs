// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.Enums;
using Bit.Core.Identity;
using Bit.Core.IdentityServer;
using Bit.Core.Repositories;
using Duende.IdentityServer.Models;
using Duende.IdentityModel;

namespace Bit.Identity.IdentityServer.ClientProviders;

internal class OrganizationClientProvider : IClientProvider
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IOrganizationApiKeyRepository _organizationApiKeyRepository;

    public OrganizationClientProvider(
        IOrganizationRepository organizationRepository,
        IOrganizationApiKeyRepository organizationApiKeyRepository
    )
    {
        _organizationRepository = organizationRepository;
        _organizationApiKeyRepository = organizationApiKeyRepository;
    }

    public async Task<Client> GetAsync(string identifier)
    {
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
}
