// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.IdentityServer;
using Bit.Core.Platform.Installations;
using Duende.IdentityServer.Models;
using Duende.IdentityModel;

namespace Bit.Identity.IdentityServer.ClientProviders;

internal class InstallationClientProvider : IClientProvider
{
    private readonly IInstallationRepository _installationRepository;

    public InstallationClientProvider(IInstallationRepository installationRepository)
    {
        _installationRepository = installationRepository;
    }

    public async Task<Client> GetAsync(string identifier)
    {
        if (!Guid.TryParse(identifier, out var installationId))
        {
            return null;
        }

        var installation = await _installationRepository.GetByIdAsync(installationId);

        if (installation == null)
        {
            return null;
        }

        return new Client
        {
            ClientId = $"installation.{installation.Id}",
            RequireClientSecret = true,
            ClientSecrets = { new Secret(installation.Key.Sha256()) },
            AllowedScopes = new[]
            {
                ApiScopes.ApiPush,
                ApiScopes.ApiLicensing,
                ApiScopes.ApiInstallation,
            },
            AllowedGrantTypes = GrantTypes.ClientCredentials,
            AccessTokenLifetime = 3600 * 24,
            Enabled = installation.Enabled,
            Claims = new List<ClientClaim>
            {
                new(JwtClaimTypes.Subject, installation.Id.ToString()),
            },
        };
    }
}
